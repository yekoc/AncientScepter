﻿using BepInEx.Configuration;
using R2API;
using RoR2;
using RoR2.Skills;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static AncientScepter.ItemHelpers;
using static AncientScepter.MiscUtil;
using static AncientScepter.SkillUtil;
using AncientScepter.ScepterSkillsMonster;

namespace AncientScepter
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
    public abstract class ScepterSkill
    {
        public abstract SkillDef myDef { get; protected set; }
        public abstract string oldDescToken { get; protected set; }
        public abstract string newDescToken { get; protected set; }
        public abstract string overrideStr { get; }

        internal abstract void SetupAttributes();

        internal virtual void LoadBehavior()
        {
        }

        internal virtual void UnloadBehavior()
        {
        }

        public abstract string targetBody { get; }
        public abstract SkillSlot targetSlot { get; }
        public abstract int targetVariantIndex { get; }
    }

    public class AncientScepterItem : ItemBase<AncientScepterItem>
    {
        public static bool engiTurretAdjustCooldown;

        public static bool engiWalkerAdjustCooldown;

        public static RerollMode rerollMode;

        public static bool artiFlamePerformanceMode;

        public static bool enableMonsterSkills;

        //public static bool enableBrotherEffects;

        //public static bool enableCommandoAutoaim;

        public static bool turretBlacklist;

        public static StridesInteractionMode stridesInteractionMode;

        public static bool captainNukeFriendlyFire;

        //TODO: test w/ stage changes
        public enum StridesInteractionMode
        {
            HeresyTakesPrecedence, ScepterTakesPrecedence, ScepterRerolls
        }

        public enum RerollMode
        {
            Disabled, Random, Scrap
        }

        public override string ItemName => "Ancient Scepter";

        public override string ItemLangTokenName => "ANCIENT_SCEPTER";

        public override string ItemPickupDesc => "Upgrades one of your skills.";



        public override string ItemFullDescription =>
            $"Upgrade one of your <style=cIsUtility>skills</style>. <style=cStack>(Unique per character)</style>"
                        + $" <style=cStack>{(rerollMode != RerollMode.Disabled ? "Extra/Unusable" : "Unusable (but NOT extra)")} pickups will reroll into {(rerollMode == RerollMode.Scrap ? "red scrap" : "other legendary items.")}</style>";


        public override string ItemLore => "Perfected energies. <He> holds it before us. The crystal of foreign elements is not attached physically, yet it does not falter from the staff's structure.\n\nOverwhelming strength. We watch as <His> might splits the ground asunder with a single strike.\n\nWondrous possibilities. <His> knowledge unlocks further pathways of development. We are enlightened by <Him>.\n\nExcellent results. From <His> hands, [Nanga] takes hold. It is as <He> said: The weak are culled.\n\nRisking everything. The crystal destabilizies. [Nanga] is gone, and <He> is forced to wield it once again.\n\nPower comes at a cost. <He> is willing to pay.";

        public override ItemTier Tier => ItemTier.Tier3;
        public override ItemTag[] ItemTags => EvaluateItemTags();

        public override GameObject ItemModel => Assets.mainAssetBundle.LoadAsset<GameObject>("mdlAncientScepterPickup");
        public override Sprite ItemIcon => Assets.mainAssetBundle.LoadAsset<Sprite>("texAncientScepterIcon");
        public override GameObject ItemDisplay => Assets.mainAssetBundle.LoadAsset<GameObject>("mdlAncientScepterDisplay");
        public override bool TILER2_MimicBlacklisted => true;

        public override bool AIBlacklisted => true;

        public static GameObject ItemBodyModelPrefab;

        public override void Init(ConfigFile config)
        {
            RegisterSkills();
            CreateConfig(config);
            CreateLang();
            CreateItem();
            Hooks();
            Install();
            //InstallLanguage();
        }

        private ItemTag[] EvaluateItemTags()
        {
            List<ItemTag> availableTags = new List<ItemTag>()
            {
                ItemTag.Utility,
                ItemTag.AIBlacklist,
            };
            if (turretBlacklist)
            {
                availableTags.Add(ItemTag.CannotCopy);
            }
            return availableTags.ToArray();
        }

        public override void CreateConfig(ConfigFile config)
        {
            engiTurretAdjustCooldown = config.Bind<bool>("Item: " + ItemName, "TR12-C Gauss Compact Faster Recharge", false, "If true, TR12-C Gauss Compact will recharge faster to match the additional stock.").Value;
            engiWalkerAdjustCooldown = config.Bind<bool>("Item: " + ItemName, "TR58-C Carbonizer Mini Faster Recharge", false, "If true, TR58-C Carbonizer Mini will recharge faster to match the additional stock.").Value;
            rerollMode = config.Bind("Item: " + ItemName, "Reroll on pickup mode", RerollMode.Random, "If \"Disabled\", this behavior will only be used for characters which cannot benefit from the item at all." +
                "\nIf \"Random\", any stacks picked up past the first will reroll to other red items." +
                "\nIf \"Scrap\", any stacks picked up past the first will reroll into red scrap.").Value;
            artiFlamePerformanceMode = config.Bind<bool>("Item: " + ItemName, "ArtiFlamePerformance", false, "If true, Dragon's Breath will use significantly lighter particle effects and no dynamic lighting.").Value;
            stridesInteractionMode = config.Bind<StridesInteractionMode>("Item: " + ItemName, "Scepter Rerolls", StridesInteractionMode.ScepterRerolls, "Changes what happens when a character whose skill is affected by Ancient Scepter has both Ancient Scepter and the corresponding heretic skill replacements (Visions/Hooks/Strides/Essence) at the same time.").Value; //defer until next stage
            enableMonsterSkills = config.Bind("Item: " + ItemName, "Enable skills for monsters", true, "If true, certain monsters get the effects of the Ancient Scepter.").Value;
            //enableBrotherEffects = config.Bind("Item: " + ItemName, "Enable Mithrix Lines", true, "If true, Mithrix will have additional dialogue when acquiring the Ancient Scepter.").Value;
            //enableCommandoAutoaim = config.Bind("Item: " + ItemName, "Enable Commando Autoaim", true, "This may break compatibiltiy with skills.").Value;
            turretBlacklist = config.Bind("Item: " + ItemName, "Blacklist Turrets", false, "If true, turrets will be blacklisted from getting the Ancient Scepter." +
                "\nIf false, they will get the scepter and will get rerolled depending on the reroll mode.").Value;
            captainNukeFriendlyFire = config.Bind("Item: " + ItemName, "Captain Nuke Friendly Fire", false, "If true, then Captain's Scepter Nuke will also inflict blight on allies.").Value;

            var engiSkill = skills.First(x => x is EngiTurret2);
            engiSkill.myDef.baseRechargeInterval = EngiTurret2.oldDef.baseRechargeInterval * (engiTurretAdjustCooldown ? 2f / 3f : 1f);
            GlobalUpdateSkillDef(engiSkill.myDef);

            var engiSkill2 = skills.First(x => x is EngiWalker2);
            engiSkill2.myDef.baseRechargeInterval = EngiWalker2.oldDef.baseRechargeInterval / (engiWalkerAdjustCooldown ? 2f : 1f);
            GlobalUpdateSkillDef(engiSkill2.myDef);
        }

        public override ItemDisplayRuleDict CreateDisplayRules()
        {
            SetupMaterials(ItemModel);
            displayPrefab = ItemDisplay;
            SetupMaterials(displayPrefab);
            var disp = displayPrefab.AddComponent<ItemDisplay>();
            disp.rendererInfos = Assets.SetupRendererInfos(displayPrefab);

            ItemDisplayRuleDict rules = new ItemDisplayRuleDict(new ItemDisplayRule[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = displayPrefab,
childName = "Pelvis",
localPos = new Vector3(0.1473F, -0.073F, -0.0935F),
localAngles = new Vector3(333.2843F, 198.8161F, 165.1177F),
localScale = new Vector3(0.2235F, 0.2235F, 0.2235F)
                }
            });

            rules.Add("mdlHuntress", new ItemDisplayRule[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = displayPrefab,
childName = "Pelvis",
localPos = new Vector3(0F, 0.0638F, 0.0973F),
localAngles = new Vector3(76.6907F, 0F, 0F),
localScale = new Vector3(0.2812F, 0.2812F, 0.2812F)
                }
            });

            rules.Add("mdlMage", new ItemDisplayRule[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = displayPrefab,
childName = "HandR",
localPos = new Vector3(-0.0021F, 0.1183F, 0.063F),
localAngles = new Vector3(0F, 34.1F, 90F),
localScale = new Vector3(0.4416F, 0.4416F, 0.4416F)
                }
            });

            rules.Add("mdlEngi", new ItemDisplayRule[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = displayPrefab,
childName = "CannonHeadR",
localPos = new Vector3(0.0186F, 0.3435F, 0.2246F),
localAngles = new Vector3(0F, 0F, 0F),
localScale = new Vector3(0.5614F, 0.5614F, 0.5614F)
                }
            });

            rules.Add("mdlMerc", new ItemDisplayRule[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = displayPrefab,
childName = "Pelvis",
localPos = new Vector3(0.1712F, 0F, 0F),
localAngles = new Vector3(69.8111F, 180F, 180F),
localScale = new Vector3(0.2679F, 0.2679F, 0.2679F)
                }
            });

            rules.Add("mdlLoader", new ItemDisplayRule[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = displayPrefab,
childName = "MechLowerArmR",
localPos = new Vector3(0.0813F, 0.4165F, -0.0212F),
localAngles = new Vector3(0F, 180F, 180F),
localScale = new Vector3(0.4063F, 0.4063F, 0.4063F)
                }
            });

            rules.Add("mdlCaptain", new ItemDisplayRule[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = displayPrefab,
childName = "Chest",
localPos = new Vector3(-0.0046F, 0.0099F, -0.286F),
localAngles = new Vector3(10.4706F, 1.6895F, 24.8468F),
localScale = new Vector3(0.4928F, 0.4928F, 0.4928F)
                }
            });

            rules.Add("mdlToolbot", new ItemDisplayRule[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = displayPrefab,
childName = "Chest",
localPos = new Vector3(1.1191F, 0.358F, -1.6717F),
localAngles = new Vector3(0F, 0F, 270F),
localScale = new Vector3(2.4696F, 2.4696F, 2.4696F)
                }
            });

            rules.Add("mdlTreebot", new ItemDisplayRule[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = displayPrefab,
childName = "CalfFrontL",
localPos = new Vector3(0F, 0.8376F, -0.1766F),
localAngles = new Vector3(0F, 0F, 0F),
localScale = new Vector3(0.8037F, 0.8037F, 0.8037F)
                }
            });

            rules.Add("mdlCroco", new ItemDisplayRule[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = displayPrefab,
childName = "MouthMuzzle",
localPos = new Vector3(0F, 2.1215F, 2.9939F),
localAngles = new Vector3(0F, 0F, 270F),
localScale = new Vector3(5.2969F, 5.2969F, 5.2969F)
                }
            });

            rules.Add("mdlBandit", new ItemDisplayRule[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = displayPrefab,
childName = "Pelvis",
localPos = new Vector3(-0.1152f, -0.1278f, 0.2056f),
localAngles = new Vector3(20F, 285F, 10F),
localScale = new Vector3(0.2235F, 0.2235F, 0.2235F)
                }
            });

            return rules;
        }

        protected override void SetupMaterials(GameObject modelPrefab)
        {
            modelPrefab.GetComponentInChildren<Renderer>().material = Assets.CreateMaterial("matAncientScepter", 1, Color.white, 1);
        }

        internal List<ScepterSkill> skills = new List<ScepterSkill>();

        public AncientScepterItem()
        {
            skills.Add(new ArtificerFlamethrower2());
            skills.Add(new ArtificerFlyUp2());
            skills.Add(new Bandit2ResetRevolver2());
            skills.Add(new Bandit2SkullRevolver2());
            skills.Add(new CaptainAirstrike2());
            skills.Add(new CaptainAirstrikeAlt2());
            skills.Add(new CommandoBarrage2());
            skills.Add(new CommandoGrenade2());
            skills.Add(new CrocoDisease2());
            skills.Add(new EngiTurret2());
            skills.Add(new EngiWalker2());
            skills.Add(new HereticNevermore2());
            skills.Add(new HuntressBallista2());
            skills.Add(new HuntressRain2());
            skills.Add(new LoaderChargeFist2());
            skills.Add(new LoaderChargeZapFist2());
            skills.Add(new MercEvis2());
            skills.Add(new MercEvisProjectile2());
            skills.Add(new ToolbotDash2());
            skills.Add(new TreebotFlower2_2());
            skills.Add(new TreebotFireFruitSeed2());

            // Monster
            if (enableMonsterSkills)
            {
                skills.Add(new AurelioniteEyeLaser2());
                skills.Add(new VultureWindblade2());
            }
        }

        public void RegisterSkills()
        {
            foreach (var skill in skills)
            {
                skill.SetupAttributes();
                RegisterScepterSkill(skill.myDef, skill.targetBody, skill.targetSlot, skill.targetVariantIndex);
            }
        }

        public override void Hooks()
        {
        }

        public void Install()
        {
            On.RoR2.CharacterBody.OnInventoryChanged += On_CBOnInventoryChanged;
            On.RoR2.CharacterMaster.GetDeployableSameSlotLimit += On_CMGetDeployableSameSlotLimit;
            On.RoR2.GenericSkill.SetSkillOverride += On_GSSetSkillOverride;

            foreach (var skill in skills)
            {
                skill.LoadBehavior();
            }

            foreach (var cm in AliveList())
            {
                if (!cm.hasBody) continue;
                var body = cm.GetBody();
                HandleScepterSkill(body);
            }
        }

        public void InstallLanguage()
        {
            foreach (var skill in skills)
            {
                if (skill.oldDescToken == null)
                {
                    continue;
                }
                languageOverlays.Add(LanguageAPI.AddOverlay(skill.newDescToken, Language.GetString(skill.oldDescToken) + skill.overrideStr));
            }
        }

        private bool handlingOverride = false;

        private void On_GSSetSkillOverride(On.RoR2.GenericSkill.orig_SetSkillOverride orig, GenericSkill self, object source, SkillDef skillDef, GenericSkill.SkillOverridePriority priority)
        {
            bool skillDefIsNotHeresy()
            {
                var skillIndex = skillDef.skillIndex;
                return (skillIndex != CharacterBody.CommonAssets.lunarPrimaryReplacementSkillDef.skillIndex
                    || skillIndex != CharacterBody.CommonAssets.lunarSecondaryReplacementSkillDef.skillIndex
                    || skillIndex != CharacterBody.CommonAssets.lunarUtilityReplacementSkillDef.skillIndex
                    || skillIndex != CharacterBody.CommonAssets.lunarSpecialReplacementSkillDef.skillIndex);
            }

            if (stridesInteractionMode != StridesInteractionMode.ScepterTakesPrecedence
                || skillDefIsNotHeresy()
                || !(source is CharacterBody body)
                || body.inventory.GetItemCount(ItemDef) < 1
                || handlingOverride)
                orig(self, source, skillDef, priority);
            else
            {
                handlingOverride = true;
                HandleScepterSkill(body);
                handlingOverride = false;
            }
        }

        private int On_CMGetDeployableSameSlotLimit(On.RoR2.CharacterMaster.orig_GetDeployableSameSlotLimit orig, CharacterMaster self, DeployableSlot slot)
        {
            var retv = orig(self, slot);
            if (slot != DeployableSlot.EngiTurret) return retv;
            var sp = self.GetBody()?.skillLocator?.special;
            if (!sp) return retv;
            if (sp.skillDef == skills.First(x => x is EngiTurret2).myDef)
                return retv + 1;
            if (sp.skillDef == skills.First(x => x is EngiWalker2).myDef)
                return retv + 2;
            return retv;
        }

        private class ScepterReplacer
        {
            public string bodyName;
            public SkillSlot slotIndex;
            public int variantIndex;
            public SkillDef replDef;
        }

        private readonly List<ScepterReplacer> scepterReplacers = new List<ScepterReplacer>();
        private readonly Dictionary<string, SkillSlot> scepterSlots = new Dictionary<string, SkillSlot>();

        public bool RegisterScepterSkill(SkillDef replacingDef, string targetBodyName, SkillSlot targetSlot, int targetVariant)
        {
            if (targetVariant < 0)
            {
                AncientScepterMain._logger.LogError("Can't register a scepter skill to negative variant index");
                return false;
            }
            if (scepterReplacers.Exists(x => x.bodyName == targetBodyName && (x.slotIndex != targetSlot || x.variantIndex == targetVariant)))
            {
                foreach (var a in scepterReplacers)
                {
                    if (a.bodyName == targetBodyName)
                    {
                        AncientScepterMain._logger.LogMessage(a.bodyName);
                    }
                    if (a.slotIndex != targetSlot)
                    {
                        AncientScepterMain._logger.LogMessage($"BB");
                    }
                    if (a.variantIndex == targetVariant)
                    {
                        AncientScepterMain._logger.LogMessage($"CC");
                    }
                }
                AncientScepterMain._logger.LogError("A scepter skill already exists for this character; can't add multiple for different slots nor for the same variant");
                return false;
            }
            scepterReplacers.Add(new ScepterReplacer { bodyName = targetBodyName, slotIndex = targetSlot, variantIndex = targetVariant, replDef = replacingDef });
            scepterSlots[targetBodyName] = targetSlot;
            return true;
        }

        private bool handlingInventory = false;

        private void On_CBOnInventoryChanged(On.RoR2.CharacterBody.orig_OnInventoryChanged orig, CharacterBody self)
        {
            orig(self);
            if (handlingInventory) return;
            handlingInventory = true;

            if (!HandleScepterSkill(self))
            {
                if (GetCount(self) > 0)
                {
                    Reroll(self, GetCount(self));
                }
            }
            else if (GetCount(self) > 1 && rerollMode != RerollMode.Disabled)
            {
                Reroll(self, GetCount(self) - 1);
            }
            handlingInventory = false;
        }

        private void Reroll(CharacterBody self, int count)
        {
            if (count <= 0) return;
            switch (rerollMode)
            {
                case RerollMode.Disabled:
                    break;
                case RerollMode.Random:
                    var list = Run.instance.availableTier3DropList.Except(new[] { PickupCatalog.FindPickupIndex(ItemDef.itemIndex) }).ToList(); //todo optimize
                    for (var i = 0; i < count; i++)
                    {
                        self.inventory.RemoveItem(ItemDef, 1);
                        self.inventory.GiveItem(PickupCatalog.GetPickupDef(list[UnityEngine.Random.Range(0, list.Count)]).itemIndex);
                    }
                    break;
                case RerollMode.Scrap:
                    for (var i = 0; i < count; i++)
                    {
                        self.inventory.RemoveItem(ItemDef, 1);
                        self.inventory.GiveItem(RoR2Content.Items.ScrapRed);
                    }
                    break;
            }
        }

        private bool HandleScepterSkill(CharacterBody self, bool forceOff = false)
        {
            bool hasHeresyForSlot(SkillSlot skillSlot)
            {
                switch (skillSlot)
                {
                    case SkillSlot.Primary:
                        return self.inventory.GetItemCount(RoR2Content.Items.LunarPrimaryReplacement) > 0;
                    case SkillSlot.Secondary:
                        return self.inventory.GetItemCount(RoR2Content.Items.LunarSecondaryReplacement) > 0;
                    case SkillSlot.Utility:
                        return self.inventory.GetItemCount(RoR2Content.Items.LunarUtilityReplacement) > 0;
                    case SkillSlot.Special:
                        return self.inventory.GetItemCount(RoR2Content.Items.LunarSpecialReplacement) > 0;
                }
                return false;
            }
            if (self.skillLocator && self.master?.loadout != null)
            {
                var bodyName = BodyCatalog.GetBodyName(self.bodyIndex);

                var repl = scepterReplacers.FindAll(x => x.bodyName == bodyName);
                if (repl.Count > 0)
                {
                    SkillSlot targetSlot = scepterSlots[bodyName];
                    if (targetSlot == SkillSlot.Utility && stridesInteractionMode == StridesInteractionMode.ScepterRerolls && hasHeresyForSlot(targetSlot)) return false;
                    var targetSkill = self.skillLocator.GetSkill(targetSlot);
                    if (!targetSkill) return false;
                    var targetSlotIndex = self.skillLocator.GetSkillSlotIndex(targetSkill);
                    var targetVariant = self.master.loadout.bodyLoadoutManager.GetSkillVariant(self.bodyIndex, targetSlotIndex);
                    var replVar = repl.Find(x => x.variantIndex == targetVariant);
                    if (replVar == null) return false;
                    if (!forceOff && GetCount(self) > 0)
                    {
                        if (stridesInteractionMode == StridesInteractionMode.ScepterTakesPrecedence && hasHeresyForSlot(targetSlot))
                        {
                            self.skillLocator.utility.UnsetSkillOverride(self, CharacterBody.CommonAssets.lunarUtilityReplacementSkillDef, GenericSkill.SkillOverridePriority.Replacement);
                        }
                        targetSkill.SetSkillOverride(self, replVar.replDef, GenericSkill.SkillOverridePriority.Upgrade);
                    }
                    else
                    {
                        targetSkill.UnsetSkillOverride(self, replVar.replDef, GenericSkill.SkillOverridePriority.Upgrade);
                        if (stridesInteractionMode == StridesInteractionMode.ScepterTakesPrecedence && hasHeresyForSlot(targetSlot))
                        {
                            self.skillLocator.utility.SetSkillOverride(self, CharacterBody.CommonAssets.lunarUtilityReplacementSkillDef, GenericSkill.SkillOverridePriority.Replacement);
                        }
                    }

                    return true;
                }
            }
            return false;
        }
    }
}