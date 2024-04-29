using Game.Achievements;
using Game.Interface;
using Game.Services;
using HarmonyLib;
using Home.HomeScene;
using Home.Messages.Incoming;
using Home.Shared;
using Server.Shared;
using Server.Shared.Extensions;
using Services;
using SML;
using System;
using System.Collections.Generic;
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
        //    Console.WriteLine($"{k}\n\t{v.Select(ach => $"{ach.Earned}\t{ach.WinNumGamesType}\t{ach.Name}").Join(delimiter: "\n\t")}");
        //}
    }

    public static void OnEarnNewAchievement(EarnedAchievementMessage message)
    {
        return;
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
        var gameFontMaterial = ApplicationController.ApplicationContext.FontControllerSource.fonts[berylium].standardFontMaterial;

        AchievementTitle1 = transform.Find("AchievementTitle1").GetComponent<TMP_Text>();
        AchievementTitle1.font = gameFont;
        AchievementTitle1.fontSharedMaterial = gameFontMaterial;
        AchievementTitle2 = transform.Find("AchievementTitle2").GetComponent<TMP_Text>();
        AchievementTitle2.font = gameFont;
        AchievementTitle2.fontSharedMaterial = gameFontMaterial;
        AchievementTitle3 = transform.Find("AchievementTitle3").GetComponent<TMP_Text>();
        AchievementTitle3.font = gameFont;
        AchievementTitle3.fontSharedMaterial = gameFontMaterial;
        AchievementTitle4 = transform.Find("AchievementTitle4").GetComponent<TMP_Text>();
        AchievementTitle4.font = gameFont;
        AchievementTitle4.fontSharedMaterial = gameFontMaterial;

        AchievementText1 = transform.Find("AchievementText1").GetComponent<TMP_Text>();
        AchievementText1.font = gameFont;
        AchievementText1.fontSharedMaterial = gameFontMaterial;
        AchievementText2 = transform.Find("AchievementText2").GetComponent<TMP_Text>();
        AchievementText2.font = gameFont;
        AchievementText2.fontSharedMaterial = gameFontMaterial;
        AchievementText3 = transform.Find("AchievementText3").GetComponent<TMP_Text>();
        AchievementText3.font = gameFont;
        AchievementText3.fontSharedMaterial = gameFontMaterial;
        AchievementText4 = transform.Find("AchievementText4").GetComponent<TMP_Text>();
        AchievementText4.font = gameFont;
        AchievementText4.fontSharedMaterial = gameFontMaterial;
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
                if (AchievementText1 is null || AchievementTitle1 is null) CacheObjects();
                AchievementTitle1.SetText(title);
                AchievementTitle1.gameObject.SetActive(true);
                AchievementTitle1.color = color;
                AchievementText1.SetText(desc);
                AchievementText1.gameObject.SetActive(true);
                AchievementText1.color = color;
                break;
            case 1:
                if (AchievementText2 is null || AchievementTitle2 is null) CacheObjects();
                AchievementTitle2.SetText(title);
                AchievementTitle2.gameObject.SetActive(true);
                AchievementTitle2.color = color;
                AchievementText2.SetText(desc);
                AchievementText2.gameObject.SetActive(true);
                AchievementText2.color = color;
                break;
            case 2:
                if (AchievementText3 is null || AchievementTitle3 is null) CacheObjects();
                AchievementTitle3.SetText(title);
                AchievementTitle3.gameObject.SetActive(true);
                AchievementTitle3.color = color;
                AchievementText3.SetText(desc);
                AchievementText3.gameObject.SetActive(true);
                AchievementText3.color = color;
                break;
            case 3:
                if (AchievementText4 is null || AchievementTitle4 is null) CacheObjects();
                AchievementTitle4.SetText(title);
                AchievementTitle4.gameObject.SetActive(ModSettings.GetBool("See Hidden Achievements"));
                AchievementTitle4.color = color;
                AchievementText4.SetText(desc);
                AchievementText4.gameObject.SetActive(ModSettings.GetBool("See Hidden Achievements"));
                AchievementText4.color = color;
                break;
            default:
                throw new Exception("Unexpected value");
        }
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
}

[HarmonyPatch(typeof(RoleCardElementsPanel))]
public class RoleCardElementsPanelPatch
{
    [HarmonyPatch(nameof(RoleCardElementsPanel.Start))]
    [HarmonyPostfix]
    public static void StartPostfix(RoleCardElementsPanel __instance)
    {
        var go = AchievementTracker.SpawnUI(__instance.gameObject);
        var controller = go.GetComponent<AchievementTrackerUIController>();

        var currentRole = Pepper.GetMyCurrentIdentity().role.ToString().ToLower();
        List<AchievementInfo> achievements = AchievementTracker.RoleToAchievements[currentRole].Filter(info => !info.WinNumGamesType);
        for (int i = 0; i < achievements.Count; i++)
        {
            controller.SetAchievementText(achievements[i].Name, achievements[i].Description, i, achievements[i].Earned);
        }
    }
}