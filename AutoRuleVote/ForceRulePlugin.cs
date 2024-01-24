using UnityEngine;
using RoR2;
using BepInEx;
using BepInEx.Configuration;
using System.Collections.Generic;
using UnityEngine.Networking;
using System;
using MonoMod.Cil;

namespace R2API.Utils
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class ManualNetworkRegistrationAttribute : Attribute
    {
    }
}

namespace AutoRuleVote
{
    [BepInPlugin("com.Moffein.ForceRule", "ForceRule", "1.0.0")]
    public class ForceRulePlugin : BaseUnityPlugin
    {
        private static ConfigEntry<string> ruleListString;
        private static ConfigEntry<int> voteCountOverride;
        //private static List<RuleChoiceDef> ruleChoiceList = new List<RuleChoiceDef>();
        private static HashSet<int> ruleChoiceIndices = new HashSet<int>();

        public void Awake()
        {
            ReadConfig();
            RoR2Application.onLoad += ParseRuleList;

            IL.RoR2.PreGameRuleVoteController.UpdateGameVotes += OverrideVotes;
        }

        private void OverrideVotes(MonoMod.Cil.ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                 x => x.MatchCall<UnityEngine.Networking.NetworkServer>("get_active")
                ))
            {
                c.EmitDelegate<Func<bool, bool>>(networkServerActive =>
                {
                    if (networkServerActive)
                    {
                        int voteCount = voteCountOverride.Value;
                        foreach (int index in ruleChoiceIndices)
                        {
                            Debug.Log("ForceRule: Overriding votes for RuleChoiceIndex " + index);
                            PreGameRuleVoteController.votesForEachChoice[index] = voteCount;
                        }
                    }
                    return networkServerActive;
                });
            }
            else
            {
                UnityEngine.Debug.LogError("ForceRule: IL Hook failed.");
            }
        }

        private void ReadConfig()
        {
            ruleListString = base.Config.Bind<string>(new ConfigDefinition("Settings", "Rule List"), "", new ConfigDescription("List of rules to force enable, separated by commas. Enter rules_list_choices in console for a full list of valid choices."));
            ruleListString.SettingChanged += RuleListString_SettingChanged;

            voteCountOverride = base.Config.Bind<int>(new ConfigDefinition("Settings", "Vote Count Override"), 100, new ConfigDescription("Overrides vote count for specified rules with this value. If rule voting is disabled, just set this to 1."));
        }

        private void RuleListString_SettingChanged(object sender, System.EventArgs e)
        {
            ParseRuleList();
        }

        //Run this after RoR2Application.onLoad when the rule catalog has been initialized
        private void ParseRuleList()
        {
            //ruleChoiceList.Clear();
            ruleChoiceIndices.Clear();
            string[] ruleArray = ruleListString.Value.Split(',');
            foreach (string str in ruleArray)
            {
                string trim = str.Trim();
                if (trim.Length > 0)
                {
                    RuleChoiceDef ruleChoice = RuleCatalog.FindChoiceDef(trim);
                    if (ruleChoice == null)
                    {
                        Debug.LogError("ForceRule: Could not find RuleChoiceDef for " + ruleChoice +", skipping.");
                        continue;
                    }

                    //ruleChoiceList.Add(ruleChoice);
                    ruleChoiceIndices.Add(ruleChoice.globalIndex);
                    Debug.Log("ForceRule: Added " + ruleChoice);
                }
            }
        }
    }
}
