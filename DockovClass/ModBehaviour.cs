using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using Duckov.Scenes;
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
    public class MapCricleSpawn
    {
        public Item LikeItem { get; set; }
        public Vector3 Position { get; set; }
        public float Radius { get; set; } = 10f;
        public int From { get; set; }
        public int Index { get; set; }
        public string BoxName { get; set; }
    }

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

        private bool CricleState = false;
        private HashSet<GameObject> QuestCircleObjects = new HashSet<GameObject>();
        //public InteractableLootbox[] AllLootboxesCache;
        public InteractablePickup[] InteractableItems;

        void Log(string msg)
        {
            Debug.Log($"> Search Crown Mod: {msg}");
        }

        void OnEnable()
        {
            Log("Enable");
            LevelManager.OnLevelInitialized += SearchCrownAfterInitialized;
            // 注册画圈事件
            View.OnActiveViewChanged += ToggleQuestCircles;
        }

        void OnDisable()
        {
            Log("Disable");
            LevelManager.OnAfterLevelInitialized -= SearchCrownAfterInitialized;
            // 注销画圈事件
            View.OnActiveViewChanged -= ToggleQuestCircles;
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
                CharacterMainControl.Main.PopText($"地上有:{item.DisplayName} 坐标({item.ActiveAgent.transform.position})");
                yield return new WaitForSeconds(3f); // 延迟 3 秒再显示下一个
            }
        }

        private void ToggleQuestCircles()
        {
            MiniMapView mapView = MiniMapView.Instance;
            if (mapView != null && View.ActiveView == mapView)
            {
                DrawQuestCircles();
            }
            else
            {
                // clear
                if (CricleState)
                {
                    ClearQuestCircles();
                    CricleState = false;
                }
            }
        }

        private void DrawQuestCircles()
        {
            if (CricleState)
            {
                return;
            }

            CricleState = true;
            // Draw circles
            ClearQuestCircles();

            // Only draw the circles in the target map
            if (SceneManager.GetActiveScene().name != MAP_NAME)
            {
                return;
            }
            InteractableItems = UnityEngine.Object.FindObjectsByType<InteractablePickup>(FindObjectsSortMode.None);
            int DrawCount = 0;
            foreach (var item in InteractableItems)
            {
                if(item?.ItemAgent?.Item != null)
                {
                    if (targets.ContainsKey(item.ItemAgent.Item.TypeID))
                    {
                        DrawCircleMark(item.ItemAgent.transform.position, 10f, item.ItemAgent.Item.DisplayName);
                        DrawCount++;
                    }
                }
            }
        }

        private void ClearQuestCircles()
        {
            foreach(GameObject cricle in QuestCircleObjects)
            {
                if (cricle != null)
                {
                    Destroy(cricle);
                }
            }

            QuestCircleObjects.Clear();
        }

        private Sprite GetQuestIcon()
        {
            List<Sprite> AllIcons = MapMarkerManager.Icons;
            if (AllIcons == null)
            {
                Debug.Log("无法获取图标。");
                return null;
            }
            if (AllIcons?.Count == null || AllIcons?.Count <= 0)
            {
                Debug.Log("图标为空");
            }
            return AllIcons[0];
        }

        private void DrawCircleMark(Vector3 position, float radius, string itemName)
        {
            GameObject obj = new GameObject($"Item_${itemName}");
            obj.transform.position = position;

            Sprite iconToUse = GetQuestIcon();
            
            try
            {
                SimplePointOfInterest poi = obj.AddComponent<SimplePointOfInterest>();
                poi.Setup(iconToUse, itemName, followActiveScene: true);

                poi.Color = Color.green;
                poi.IsArea = true;
                poi.AreaRadius = radius;
                poi.ShadowColor = Color.grey;
                poi.ShadowDistance = 0f;

                if (MultiSceneCore.MainScene.HasValue)
                {
                    SceneManager.MoveGameObjectToScene(obj, MultiSceneCore.MainScene.Value);
                }

                QuestCircleObjects.Add(obj);

            } catch (Exception e)
            {
                Debug.LogError($"异常失败: {e.Message}");
                Destroy(obj);
            }
        }
    }
}