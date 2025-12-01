using Duckov.UI;
using Duckov.Utilities;
using ItemStatsSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace InspectTheFootRoom
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private static int CROWN_ID = 1254, X_KEY = 827, O_KEY = 828, YELLOW_CARD = 801, RED_CARD = 802, GREEN_CARD = 803, BLUE_CARD = 804, BLACK_CARD = 886, PURPLE_CARD = 887;
        private static readonly Dictionary<int, string> targets = new Dictionary<int, string>()
        {
            { CROWN_ID, "皇冠" },
            { X_KEY, "X钥匙" },
            { O_KEY, "O钥匙" },
            { YELLOW_CARD, "黄卡" },
            { RED_CARD, "红卡" },
            { GREEN_CARD, "绿卡" },
            { BLUE_CARD, "蓝卡" },
            { BLACK_CARD, "黑卡" },
            { PURPLE_CARD, "紫卡" },
        };
        private static string MAP_NAME = "Level_Farm_01";

        void Log(string msg)
        {
            Debug.Log($"> Search Crown Mod: {msg}");
        }

        void OnEnable()
        {
            Log("Enable");
            LevelManager.OnLevelInitialized += SearchCrownAfterInitialized;
        }

        void OnDisable()
        {
            Log("Disable");
            LevelManager.OnAfterLevelInitialized -= SearchCrownAfterInitialized;
        }

        private void SearchCrownAfterInitialized()
        {
            string scene_name = SceneManager.GetActiveScene().name;
            Log($"OnLevelInitialized: {scene_name}");

            if (scene_name != MAP_NAME)
            {
                return;
            }

            HashSet<int> self_items = new HashSet<int>();
            foreach (var item in LevelManager.Instance?.PetProxy?.Inventory)
            {
                self_items.Add(item.GetInstanceID());
            }
            foreach (var item in LevelManager.Instance?.MainCharacter?.CharacterItem?.Inventory)
            {
                self_items.Add(item.GetInstanceID());
            }

            List<ItemStatsSystem.Item> items = new List<ItemStatsSystem.Item>();
            bool found = false;
            foreach(var item in UnityEngine.Object.FindObjectsByType<ItemStatsSystem.Item>(FindObjectsSortMode.None)){
                int item_type_id = item.TypeID;

                if (self_items.Contains(item.GetInstanceID()))
                {
                    continue;
                }

                if (item.FromInfoKey != "Ground")
                {
                    //Log($"--忽略不是地上的物品: {item.DisplayName} ({item_type_id}) {item.GetInstanceID()} {item.FromInfoKey}");
                    continue;
                }

                if (targets.ContainsKey(item_type_id))
                {
                    items.Add((item));
                    Log($"{item.DisplayName} ({item_type_id}) {item.GetInstanceID()} {item.FromInfoKey} - {item.ActiveAgent.transform.position}");

                    if (item_type_id == CROWN_ID && found is false)
                    {
                        found = true;
                    }
                }
            }

            if (items.Count == 0) {
                StartCoroutine(ShowPoorMessages());
                return;
            }

            StartCoroutine(ShowItemsOnGround(items, found));  
        }

        private IEnumerator ShowPoorMessages()
        {
            CharacterMainControl.Main.PopText("什么都木有!");
            yield return new WaitForSeconds(3f); 
            CharacterMainControl.Main.PopText("什么都木有!!");
            yield return new WaitForSeconds(2f);
            CharacterMainControl.Main.PopText("什么都木有!!!");
        }

        private IEnumerator ShowItemsOnGround(List<ItemStatsSystem.Item> items, bool found)
        {
            if (found)
            {
                CharacterMainControl.Main.PopText("找到你了!");
                yield return new WaitForSeconds(2f); // 延迟 3 秒再显示下一个
            }            

            foreach (var item in items)
            {
                CharacterMainControl.Main.PopText($"{item.DisplayName} P:{item.ActiveAgent.transform.position}");
                yield return new WaitForSeconds(3f); // 延迟 3 秒再显示下一个
            }
        }
    }
}