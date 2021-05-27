using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Monsters;

namespace HealthBars
{
    [SuppressMessage("ReSharper", "SuggestVarOrType_BuiltInTypes")]
    public class HealthBars : Mod
    {
        /// <summary>The width of the healthbar border in **texture pixels**</summary>
        private int _healthbarBorderWidth;

        /// <summary>The height of the healthbar border in **texture pixels**</summary>
        private int _healthbarBorderHeight;

        /// <summary>The width of the health bar in **texture pixels**</summary>
        private int _healthbarWidth;

        /// <summary>The height of the enemy health bars in **texture pixels**</summary>
        private int _healthbarHeight;

        /// <summary>The offset of the healthbar above monsters in **texture pixels**</summary>
        private int _healthbarOffset;


        /// <summary>The configuration of the mod</summary>
        private HealthBarsConfig _config;


        /// <summary>Texture of the healthbar border</summary>
        private Texture2D _healthbarBorderTexture;

        /// <summary>Texture of the healthbar background</summary>
        private Texture2D _healthbarTexture;

        /// <summary>Font of the healthbar text</summary>
        private SpriteFont _healthTextFont;


        /// <summary>Field info for rock crabs' private shellGone property;
        /// needed to hide rock crabs' health bar when in rock form</summary>
        private static readonly FieldInfo RockCrabShellGoneFieldInfo =
            typeof(RockCrab).GetField("shellGone", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>Field info for rock golems' seen player property</summary>
        private static readonly FieldInfo RockGolemSeenPlayerFieldInfo =
            typeof(RockGolem).GetField("seenPlayer", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>Field info for bat's seen player property</summary>
        private static readonly FieldInfo BatSeenPlayerFieldInfo =
            typeof(Bat).GetField("seenPlayer", BindingFlags.Instance | BindingFlags.NonPublic);


        /// <summary>
        /// The main entry function of the mod. This gets executed once the mod is loaded
        /// by the game.
        /// </summary>
        /// <param name="helper">The modding api helper</param>
        public override void Entry(IModHelper helper)
        {
            Monitor.Log("Starting HealthBars Mod", LogLevel.Info);


            // Loading config values   
            Monitor.Log("Loading config", LogLevel.Debug);
            _config = Helper.ReadConfig<HealthBarsConfig>();
            _healthbarOffset = -_config.HealthbarOffset;


            // Loading textures and assets
            Monitor.Log("Loading Textures", LogLevel.Debug);
            _healthbarBorderTexture = helper.Content.Load<Texture2D>("assets/healthbar_border_w2.png");
            _healthbarBorderWidth = _healthbarBorderTexture.Width;
            _healthbarBorderHeight = _healthbarBorderTexture.Height;
            _healthbarWidth = _healthbarBorderWidth - 4;
            _healthbarHeight = _healthbarBorderHeight - 4;
            _healthbarTexture = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
            _healthbarTexture.SetData(new[] {Color.White});


            // Verify the reflection field type; in the case that the type is changed we know about it first
            Monitor.Log("Verifying reflection field integrity", LogLevel.Debug);
            VerifyReflectionFields(new Dictionary<FieldInfo, Type>
            {
                {RockCrabShellGoneFieldInfo, typeof(NetBool)},
                {RockGolemSeenPlayerFieldInfo, typeof(NetBool)},
                {BatSeenPlayerFieldInfo, typeof(NetBool)}
            });


            // Register the event handlers
            Monitor.Log("Registering event handlers", LogLevel.Debug);
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
        }


        /// <summary>
        /// Event handler function for when the world is rendered.
        /// It collects all the monsters in the player location and draws health bars above them
        /// as long as the player can see them.
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="args">The event arguments; contains the <c>SpriteBatch</c> to draw the health bar on</param>
        private void OnRenderedWorld(object sender, RenderedWorldEventArgs args)
        {
            // Debug helpers - let us enter the mines as quickly as possible;
            Game1.player.Speed = 20;
            Game1.player.health = Game1.player.maxHealth;

            if (_healthTextFont == null)
                _healthTextFont = Game1.smallFont;

            // Get all the characters and if it is a monster, get the bounding box of the sprite to align
            // the health bar
            var characters = Game1.currentLocation.getCharacters();
            foreach (NPC character in characters)
            {
                // Characters that aren't monsters have no health bar
                if (!(character is Monster monster)) continue;

                // If the player can't see the monster, the health bar shouldn't be drawn
                if (!PlayerCanSeeHealth(monster)) continue;

                // current bounding box of the sprite of the monster
                Rectangle boundingBoxSprite = new Rectangle(
                    (int) monster.getLocalPosition(Game1.viewport).X,
                    (int) monster.getLocalPosition(Game1.viewport).Y -
                    monster.Sprite.SpriteHeight * Game1.pixelZoom / 2,
                    monster.Sprite.SpriteWidth * Game1.pixelZoom,
                    monster.Sprite.SpriteHeight * Game1.pixelZoom);

                // health bar border rectangle
                Rectangle healthbarBorderRectangle = new Rectangle(
                    boundingBoxSprite.Center.X - _healthbarBorderWidth * Game1.pixelZoom / 2,
                    boundingBoxSprite.Y + (GetHealthbarOffset(monster) - _healthbarBorderHeight) * Game1.pixelZoom,
                    _healthbarBorderWidth * Game1.pixelZoom,
                    _healthbarBorderHeight * Game1.pixelZoom);

                // sometimes monsters have less max health than health for some reason, we have to adjust
                // TODO: Check out if there is another way, right now we're editing actual monster properties
                if (monster.MaxHealth < monster.Health)
                    monster.MaxHealth = monster.Health;
                float healthPercentage = (float) monster.Health / monster.MaxHealth;

                // adjust healthbar so it represents monster health
                float adjustedHealthbarWidth = _config.HealthbarIsPixelAligned
                    ? Math.Max(1, (int) (_healthbarWidth * healthPercentage))
                    : _healthbarWidth * healthPercentage;

                // actual healthbar rectangle, reposition / resize it so the healthbar stays inside the bar
                Rectangle healthbarRectangle = new Rectangle(
                    healthbarBorderRectangle.X + 2 * Game1.pixelZoom,
                    healthbarBorderRectangle.Y + 2 * Game1.pixelZoom,
                    (int) (adjustedHealthbarWidth * Game1.pixelZoom),
                    _healthbarHeight * Game1.pixelZoom);

                // draw health bar border and health bar to the screen
                args.SpriteBatch.Draw(_healthbarTexture, healthbarRectangle, GetHealthColor(healthPercentage));
                args.SpriteBatch.Draw(_healthbarBorderTexture, healthbarBorderRectangle, Color.White);

                
                // only draw the health text if specified so in the config
                if (!_config.ShowHealthNumbers) continue;
                
                // the health text (current health / max health)
                string healthText = $"{monster.Health}/{monster.MaxHealth}";
                
                // calculate best text size to fit the text into the health bar
                Vector2 textSize = _healthTextFont.MeasureString(healthText);
                float textScalingFitWidth = _healthbarWidth * Game1.pixelZoom * 1.2f / textSize.X;
                float textScalingFitHeight = _healthbarHeight * Game1.pixelZoom * 1.2f / textSize.Y;
                float textSizeScaling = Math.Min(textScalingFitWidth, textScalingFitHeight);
                
                // calculate centered position of the text inside the health bar
                int textOffsetLeftPixels =
                    (int) ((_healthbarWidth * Game1.pixelZoom - textSize.X * textSizeScaling) / 2);
                int textOffsetTopPixels =
                    (int) ((_healthbarHeight * Game1.pixelZoom - textSize.Y * textSizeScaling) / 2);
                Vector2 textPosition = new Vector2(healthbarRectangle.X + textOffsetLeftPixels,
                    healthbarRectangle.Y + textOffsetTopPixels);
                    
                // draw health text to screen
                args.SpriteBatch.DrawString(_healthTextFont, healthText, textPosition, new Color(86, 22, 12), 0f,
                    Vector2.Zero, textSizeScaling, SpriteEffects.None, 0);

            }
        }


        /// <summary>
        /// Detects if the player can see the monster. This accounts for if the monster is invisible
        /// or a special monster (eg. rock crab) that can not be seen immediately to avoid revealing
        /// monster locations too early. Offscreen monsters are considered seen as long as not
        /// invisible to correctly display health bars of semi-offscreen monsters.
        /// </summary>
        /// <param name="monster">The monster</param>
        /// <returns>If the player can see the monster</returns>
        private static bool PlayerCanSeeHealth(Monster monster)
        {
            switch (monster)
            {
                case RockCrab crab:
                {
                    bool shellGone = ((NetBool) RockCrabShellGoneFieldInfo.GetValue(crab)).Value;
                    return shellGone || crab.isMoving();
                }
                case RockGolem golem:
                    return ((NetBool) RockGolemSeenPlayerFieldInfo.GetValue(golem)).Value;
                case Bat bat:
                    return ((NetBool) BatSeenPlayerFieldInfo.GetValue(bat)).Value;
                default:
                    return !monster.isInvisible;
            }
        }

        /// <summary>
        /// Calculates the healthbar height offset for a monster depending on monster type
        /// to adjust it to a better position. Some monsters bodies are not properly aligned
        /// with their bounding box
        /// TODO: Probably put this into the config and make a dictionary out of it; traverse the map here
        /// </summary>
        /// <param name="monster">The monster</param>
        /// <returns>The health bar offset of a monster in **texture pixels**</returns>
        private int GetHealthbarOffset(Monster monster)
        {
            int monsterOffset = 0;
            
            if (monster is AngryRoger || monster is Ghost)
                monsterOffset = -5;
            if (monster is Bat)
                monsterOffset = 9;
            if (monster is BigSlime)
                monsterOffset = 1;
            if (monster is BlueSquid)
                monsterOffset = 5;
            if (monster is Bug)
                monsterOffset = -10;
            if (monster is DinoMonster)
                monsterOffset = 1;
            if (monster is DustSpirit)
                monsterOffset = 12;
            if (monster is DwarvishSentry)
                monsterOffset = -6;
            if (monster is Fly)
                monsterOffset = -3;
            if (monster is GreenSlime)
                monsterOffset = 4;
            if (monster is Grub)
                monsterOffset = 6;
            if (monster is HotHead)
                monsterOffset = 0;
            if (monster is LavaCrab || monster is RockCrab)
                monsterOffset = 2;
            if (monster is LavaLurk)
                monsterOffset = 0;
            if (monster is Leaper)
                monsterOffset = 6;
            if (monster is MetalHead)
                monsterOffset = 0;
            if (monster is Mummy)
                monsterOffset = -3;
            if (monster is RockGolem)
                monsterOffset = -5;
            if (monster is Serpent)
                monsterOffset = 12;
            if (monster is ShadowBrute)
                monsterOffset = -4;
            if (monster is ShadowShaman)
                monsterOffset = -6;
            if (monster is Shooter)
                monsterOffset = -4;
            if (monster is Skeleton)
                monsterOffset = -5;
            if (monster is Spiker)
                monsterOffset = 4;
            if (monster is SquidKid)
                monsterOffset = -5;

            return monsterOffset + _healthbarOffset;
        }


        /// <summary>
        /// Get the color of the health bar depending on the fraction of
        /// remaining health left. Generates a color from a color gradient from
        /// red to yellow while health is under 50% and then from yellow to green from
        /// 50% to 100% respectively.
        /// </summary>
        /// <param name="healthPercentage">The health percentage; ranging from 0 to 1</param>
        /// <returns>A color representing the health percentage</returns>
        private static Color GetHealthColor(float healthPercentage)
        {
            return healthPercentage > 0.5f
                ? new Color(1f - 1f * (healthPercentage - 0.5f), 1f, 0f)
                : new Color(1f, 1f * healthPercentage * 2f, 0f);
        }


        /// <summary>
        /// Verifies all reflection <c>FieldInfo</c> types to keep the integrity
        /// TODO: Check, if this is really necessary
        /// </summary>
        /// <param name="fields">The fields and their assumed type</param>
        private void VerifyReflectionFields(Dictionary<FieldInfo, Type> fields)
        {
            foreach (var item in fields.Where(item => item.Key.FieldType != item.Value))
            {
                Monitor.Log($"Field {item.Key.Name} of {item.Key.DeclaringType} was expected to be " +
                            $"{item.Value}, instead found {item.Key.FieldType}", LogLevel.Error);
            }
        }
    }
}