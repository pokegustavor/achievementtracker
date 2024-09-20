using Game.Achievements;
using Game.Interface;
using Game.Services;
using HarmonyLib;
using Home.HomeScene;
using Home.Messages.Incoming;
using Home.Shared;
using Server.Shared;
using Server.Shared.Extensions;
using Server.Shared.Info;
using Services;
using SML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace achievementtracker;

public class AchievementInfo
{
    public string InternalName { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public Achievement Achievement { get; set; }
    public bool WinNumGamesType => Regex.Match(Description, @"Win.+games?").Success;
    public bool Earned { get; set; }
}

[Mod.SalemMod]
[Mod.SalemMenuItem]
[SML.DynamicSettings]
public class AchievementTracker
{
    // role name in lowercase -> achievement[]
    public static Dictionary<string, List<AchievementInfo>> RoleToAchievements = new();
    public static GameObject achievementTrackerGO;
    public static bool isIncompatibleLobby
    {
        get 
        {
            Lazy<bool> isModded = new Lazy<bool>(() =>
            {
                return BetterTOS2.BTOSInfo.IS_MODDED;
            });
            return ModStates.IsEnabled("curtis.tuba.better.tos2") && isModded.Value;
        }
    }

    public static void Start()
    {
        Console.WriteLine("achievementtracker works!");
    }

    public ModSettings.CheckboxSetting SeeHiddenAchievements
    {
        get
        {
            return new()
            {
                Name = "See Hidden Achievements",
                Description = "Whether or not to see hidden achievements",
                DefaultValue = false,
                Available = true,
                AvailableInGame = true,
                OnChanged = (val) =>
                {
                    if (achievementTrackerGO is not null)
                        achievementTrackerGO.GetComponent<AchievementTrackerUIController>().ShowAchievementText(3, val);
                }
            };
        }
    }

    public static GameObject SpawnUI(GameObject parent)
    {
        GameObject achievementTrackerUIGO = UnityEngine.Object.Instantiate(FromAssetBundle.LoadGameObject("achievementtracker.resources.achievementtrackerui", "AchievementTrackerUIGO"));
        achievementTrackerGO = achievementTrackerUIGO;
        achievementTrackerUIGO.transform.SetParent(parent.transform, false);
        achievementTrackerUIGO.transform.localPosition = new Vector3(0f, 0f, 0f);
        achievementTrackerUIGO.transform.SetPositionAndRotation(new Vector3(1.2f, 0.25f, 0f), new Quaternion());
        achievementTrackerUIGO.transform.localScale = new Vector3(3f, 3f, 3f);
        achievementTrackerUIGO.transform.SetAsLastSibling();
        achievementTrackerUIGO.AddComponent<AchievementTrackerUIController>().Init();
        return achievementTrackerUIGO;
    }

    // Info about all the achievements the client has earned sent when they log in
    public static void OnEarnedAchievements(IncomingHomeMessage message)
    {
        EarnedAchievementsMessage earnedAchievementsMessage = (EarnedAchievementsMessage)message;
        var earnedAchievements = earnedAchievementsMessage.Data.AchievementsOwned;

        foreach (var achievementInfoList in RoleToAchievements.Values)
        {
            foreach (var achievementInfo in achievementInfoList)
            {
                if (earnedAchievements.Contains(achievementInfo.Achievement.id))
                    achievementInfo.Earned = true;
            }
        }

        //foreach (var (k, v) in AchievementTracker.RoleToAchievements)
        //{
        //    Console.WriteLine($"{k}\n\t{v.Select(ach => $"{ach.Earned}\t{ach.WinNumGamesType}\t{ach.Name}\n\t\t{ach.Description}").Join(delimiter: "\n\t")}");
        //}
    }

    // Info about any new acheivements the client earns during a game
    public static void OnEarnNewAchievement(IncomingHomeMessage message)
    {
        EarnedAchievementMessage m = (EarnedAchievementMessage)message;
        foreach (var achievementInfoList in RoleToAchievements.Values)
        {
            foreach (var achievementInfo in achievementInfoList)
            {
                if (achievementInfo.Achievement.id == m.Data.AchievementID)
                    achievementInfo.Earned = true;
            }
        }

        // Update -- strikethrough achievement
        if (Service.Game.Sim.simulation != null)
        {
            var role = Pepper.GetMyCurrentIdentity().role.ToString().ToLower();
            var controller = achievementTrackerGO.GetComponent<AchievementTrackerUIController>();

            if (role == controller.trackedRole)
            {
                List<AchievementInfo> achievements = RoleToAchievements[role].Filter(info => !info.WinNumGamesType);
                for (int i = 0; i < achievements.Count; i++)
                {
                    if (achievements[i].Achievement.id == m.Data.AchievementID && ShouldShowAchievementChange(m.Data.AchievementID))
                        controller.SetAchievementText(achievements[i].Name, achievements[i].Description, i, achievements[i].Earned);
                }
            }
        }
    }

    // Does not update (strikethrough change) if achievement should not be shown during gameplay
    internal static bool ShouldShowAchievementChange(int achievementId)
    {
        return !AchievementData.achievementsToHide.Contains((AchievementData.Achievements)achievementId);
    }
}

[HarmonyPatch(typeof(AchievementService))]
public class AchievementServicePatch
{
    [HarmonyPatch(nameof(AchievementService.Init))]
    [HarmonyPostfix]
    public static void InitPostfix(AchievementService __instance)
    {
        foreach (var achievement in __instance.achievementBook_.Achievements)
        {
            // seems that RoleCard_{Role} is a way to identify which role an achievement belongs to
            var role = achievement.roleImage.name.ToLower();
            // but there is also stuff like general_win and general_time
            if (role.Contains("rolecard_"))
                role = role.Substring("rolecard_".Length);

            if (!AchievementTracker.RoleToAchievements.ContainsKey(role))
                AchievementTracker.RoleToAchievements[role] = new List<AchievementInfo>();

            var info = new AchievementInfo
            {
                Achievement = achievement,
                InternalName = AchievementData.achievements[achievement.id],
                Name = Service.Home.LocalizationService.GetLocalizedString("GUI_ACHIEVEMENT_TITLE_" + achievement.id),
                Description = Service.Home.LocalizationService.GetLocalizedString("GUI_ACHIEVEMENT_DESC_" + achievement.id),
            };

            AchievementTracker.RoleToAchievements[role].Add(info);
        }

        //foreach (var (k, v) in AchievementTracker.RoleToAchievements)
        //{
        //    Console.WriteLine($"{k} -- {v.Select(ach => ach.Name).Join()}");
        //}
    }
}

[HarmonyPatch(typeof(NetworkService))]
public class NetworkServicePatch
{
    [HarmonyPatch(nameof(NetworkService.Init))]
    [HarmonyPostfix]
    public static void StartPostfix()
    {
        Service.Home.NetworkService.RegisterCallback(Home.IncomingMessageType.AlreadyEarnedAchievements, AchievementTracker.OnEarnedAchievements);
    }
}

[HarmonyPatch(typeof(HomeSceneController))]
public class HomeScenePatch
{
    public static HomeSceneController homeSceneController;

    [HarmonyPatch(nameof(HomeSceneController.Start))]
    [HarmonyPostfix]
    public static void StartPostfix(HomeSceneController __instance)
    {
        homeSceneController = __instance;
    }
}

public class AchievementTrackerUIController : MonoBehaviour
{
    TMP_Text AchievementTitle1;
    TMP_Text AchievementTitle2;
    TMP_Text AchievementTitle3;
    TMP_Text AchievementTitle4;
    TMP_Text AchievementText1;
    TMP_Text AchievementText2;
    TMP_Text AchievementText3;
    TMP_Text AchievementText4;
    public String trackedRole;

    private void Start()
    {
    }

    public void Init()
    {
        CacheObjects();
    }

    private void OnDestroy()
    {
        AchievementTracker.achievementTrackerGO = null;
        Destroy(gameObject);
    }

    private void CacheObjects()
    {
        int berylium = 0;
        var gameFont = ApplicationController.ApplicationContext.FontControllerSource.fonts[berylium].tmp_FontAsset;
        // I have no idea how else to get a reference to this material
        var gameFontMaterial = Resources.LoadAll<Material>("").Single(m => m.name == "Game SDF - Black Outline Shadowed Thick");


        AchievementTitle1 = transform.Find("AchievementTitle1").GetComponent<TMP_Text>();
        AchievementText1 = transform.Find("AchievementText1").GetComponent<TMP_Text>();
        AchievementTitle2 = transform.Find("AchievementTitle2").GetComponent<TMP_Text>();
        AchievementText2 = transform.Find("AchievementText2").GetComponent<TMP_Text>();
        AchievementTitle3 = transform.Find("AchievementTitle3").GetComponent<TMP_Text>();
        AchievementText3 = transform.Find("AchievementText3").GetComponent<TMP_Text>();
        AchievementTitle4 = transform.Find("AchievementTitle4").GetComponent<TMP_Text>();
        AchievementText4 = transform.Find("AchievementText4").GetComponent<TMP_Text>();

        quickSetAchievementMetadata(ref AchievementTitle1, ref AchievementText1, gameFont, gameFontMaterial);
        quickSetAchievementMetadata(ref AchievementTitle2, ref AchievementText2, gameFont, gameFontMaterial);
        quickSetAchievementMetadata(ref AchievementTitle3, ref AchievementText3, gameFont, gameFontMaterial);
        quickSetAchievementMetadata(ref AchievementTitle4, ref AchievementText4, gameFont, gameFontMaterial);
    }

    internal void quickSetAchievementMetadata(ref TMP_Text title, ref TMP_Text text, TMP_FontAsset font, Material material)
    {
        title.font = font;
        text.font = font;
        title.fontSharedMaterial = material;
        text.fontSharedMaterial = material;
    }

    public void SetAchievementText(string title, string desc, int index, bool earned)
    {
        Color color = Color.white;
        if (earned)
        {
            title = $"<s>{title}</s>";
            desc = $"<s>{desc}</s>";
            color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
        }

        switch (index)
        {
            case 0:
                quickSetAchievementData(ref AchievementTitle1, ref AchievementText1, title, desc, color);
                break;
            case 1:
                quickSetAchievementData(ref AchievementTitle2, ref AchievementText2, title, desc, color);
                break;
            case 2:
                quickSetAchievementData(ref AchievementTitle3, ref AchievementText3, title, desc, color);
                break;
            case 3:
                bool should_become_active = ModSettings.GetBool("See Hidden Achievements");
                quickSetAchievementData(ref AchievementTitle4, ref AchievementText4, title, desc, color, should_become_active);
                break;
            default:
                throw new Exception("Unexpected value");
        }
    }

    internal void quickSetAchievementData(ref TMP_Text title, ref TMP_Text text, string str_title, string desc, Color color, bool should_be_active = true)
    {
        if (title is null || text is null) CacheObjects();

        title.SetText(str_title);
        title.gameObject.SetActive(should_be_active);
        title.color = color;

        text.SetText(desc);
        text.gameObject.SetActive(should_be_active);
        text.color = color;
    }

    public void ShowAchievementText(int index, bool show)
    {
        switch (index)
        {
            case 0:
                AchievementText1.gameObject.SetActive(show);
                AchievementTitle1.gameObject.SetActive(show);
                break;
            case 1:
                AchievementText2.gameObject.SetActive(show);
                AchievementTitle2.gameObject.SetActive(show);
                break;
            case 2:
                AchievementText3.gameObject.SetActive(show);
                AchievementTitle3.gameObject.SetActive(show);
                break;
            case 3:
                AchievementText4.gameObject.SetActive(show);
                AchievementTitle4.gameObject.SetActive(show);
                break;
            default:
                throw new Exception("Unexpected value");
        }
    }

    internal void DisableAllAcheivement()
    {

        AchievementText1.gameObject.SetActive(false);
        AchievementTitle1.gameObject.SetActive(false);
        AchievementText2.gameObject.SetActive(false);
        AchievementTitle2.gameObject.SetActive(false);
        AchievementText3.gameObject.SetActive(false);
        AchievementTitle3.gameObject.SetActive(false);
        AchievementText4.gameObject.SetActive(false);
        AchievementTitle4.gameObject.SetActive(false);
    }
}

[HarmonyPatch(typeof(RoleCardElementsPanel))]
public class RoleCardElementsPanelPatch
{
    [HarmonyPatch(nameof(RoleCardElementsPanel.Start))]
    [HarmonyPostfix]
    public static void StartPostfix(RoleCardElementsPanel __instance)
    {
        if (AchievementTracker.isIncompatibleLobby)
            return;

        var go = AchievementTracker.SpawnUI(__instance.gameObject);
        var controller = go.GetComponent<AchievementTrackerUIController>();

        var currentRole = Pepper.GetMyCurrentIdentity().role.ToString().ToLower();
        List<AchievementInfo> achievements = AchievementTracker.RoleToAchievements[currentRole].Filter(info => !info.WinNumGamesType);
        for (int i = 0; i < achievements.Count; i++)
        {
            controller.SetAchievementText(achievements[i].Name, achievements[i].Description, i, achievements[i].Earned);
        }
        controller.trackedRole = currentRole;

        StateProperty<PlayerIdentityData> myIdentity = Service.Game.Sim.simulation.myIdentity;
        myIdentity.OnChanged = (Action<PlayerIdentityData>)Delegate.Combine(myIdentity.OnChanged, new Action<PlayerIdentityData>(HandleOnMyIdentityChanged));
    }

    [HarmonyPatch(nameof(RoleCardElementsPanel.OnDestroy))]
    [HarmonyPostfix]
    public static void OnDestroyPostfix()
    {
        // won't have created action so no need to remove it
        if (AchievementTracker.isIncompatibleLobby)
            return;

        StateProperty<PlayerIdentityData> myIdentity = Service.Game.Sim.simulation.myIdentity;
        myIdentity.OnChanged = (Action<PlayerIdentityData>)Delegate.Remove(myIdentity.OnChanged, new Action<PlayerIdentityData>(HandleOnMyIdentityChanged));
    }

    public static void HandleOnMyIdentityChanged(PlayerIdentityData data)
    {
        if (AchievementTracker.isIncompatibleLobby)
            return;

        var controller = AchievementTracker.achievementTrackerGO.GetComponent<AchievementTrackerUIController>();
        var currentRole = data.role.ToString().ToLower();
        if (Service.Game.Sim.simulation != null && currentRole != controller.trackedRole)
        {
            // Stop showing all tracked achievements incase there are no achievements for the new role
            const int MAX_ACHIEVEMENTS_PER_ROLE = 4;
            for (int i = 0; i < MAX_ACHIEVEMENTS_PER_ROLE; i++)
            {
                controller.ShowAchievementText(i, false);
            }

            List<AchievementInfo> achievements = AchievementTracker.RoleToAchievements[currentRole].Filter(info => !info.WinNumGamesType);
            for (int i = 0; i < achievements.Count; i++)
            {
                controller.SetAchievementText(achievements[i].Name, achievements[i].Description, i, achievements[i].Earned);
                controller.ShowAchievementText(i, true);
            }

            controller.trackedRole = currentRole;
        }
    }
}