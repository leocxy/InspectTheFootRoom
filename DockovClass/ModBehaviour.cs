using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using Duckov.Scenes;
using Duckov.UI;
using ItemStatsSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        private static readonly HashSet<string> RAID_MAPS = new HashSet<string>
        {
            // 农场区域
            "Level_Farm_01",
            "Level_Farm_Main",
            "Level_Farm_JLab_Facility", // 农场实验室
    
            // 地面零区
            "Level_GroundZero_Main",
            "Level_GroundZero_1",
            "Level_GroundZero_Cave",
    
            // 隐藏仓库
            "Level_HiddenWarehouse_Main",
            "Level_HiddenWarehouse",
            "Level_HiddenWarehouse_CellarUnderGround", // 地下酒窖
    
            // 实验室区域
            "Level_JLab_Main",
            "Level_JLab_1",
            "Level_JLab_2",
    
            // 沙漠区域
            "Level_Desert_Main",
            "Level_Desert",
            "Level_Desert_Boss", // 沙漠Boss房
    
            // 风暴区域（高价值，黑卡/紫卡常出）
            "Level_StormZone_Main",
            "Level_StormZone_1",
            "Level_StormZone_B0",
            "Level_StormZone_B1",
            "Level_StormZone_B2",
            "Level_StormZone_B3",
            "Level_StormZone_B4",
    
            // 雪地军事基地（新版本高价值区域）
            "Level_SnowMilitaryBase_Main",
            "Level_SnowMilitaryBase",
            "Level_SnowFactory",
            "Level_SnowMilitaryBase_ColdStorage_Main", // 冷库（必出金）
            "Level_SnowMilitaryBase_ColdStorage",
    
            // 僵尸模式（可选，如果你要支持）
            "Level_Zombie_Main",
            "Level_Zombie_1",
    
            // 风暴过去（新区域）
            "Level_StormPast_Main",
            "Level_StormPast_1",
    
            // 生存挑战（可选）
            "Level_SurivalChallenge_Main",
            "Level_SurivalChallenge",
        };

        // 判断当前是否为需要扫描的有效 Raid 战斗场景
        private bool IsValidRaidScene()
        {
            string sceneName = SceneManager.GetActiveScene().name;

            // 兜底：非战斗场景直接拒绝
            if (sceneName.StartsWith("LoadingScreen") ||
                sceneName.StartsWith("MainMenu") ||
                sceneName.StartsWith("Startup") ||
                sceneName.StartsWith("Base") ||
                sceneName.StartsWith("PREPARE") ||
                sceneName.Contains("CutScene") ||
                sceneName.Contains("Guide") ||
                sceneName.Contains("Demo") ||
                sceneName.Contains("Dream") ||
                sceneName.Contains("Wakeup") ||
                sceneName.Contains("Getout"))
            {
                Log($"[Scene] 跳过非战斗场景: {sceneName}");
                return false;
            }

            // 只扫描白名单里的战斗地图
            if (!RAID_MAPS.Contains(sceneName))
            {
                Log($"[Scene] 跳过非目标战斗场景: {sceneName}");
                return false;
            }

            Log($"[Scene] ✅ 进入目标战斗场景: {sceneName}");
            return true;
        }

        private bool CricleState = false;
        private HashSet<GameObject> QuestCircleObjects = new HashSet<GameObject>();
        //public InteractableLootbox[] AllLootboxesCache;
        public InteractablePickup[] InteractableItems;

        void Log(string msg)
        {
            Debug.Log($"[InspectTheFootRoom]: {msg}");
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
            Log("SearchCrownAfterInitialized START");

            // 1. 核心单例安全检查
            if (LevelManager.Instance == null)
            {
                Log("ERROR: LevelManager.Instance is NULL! Aborting search.");
                return;
            }

            if (!IsValidRaidScene())
                return;

            HashSet<int> self_items = new HashSet<int>();

            // 2. 安全读取宠物背包 (PetProxy 可能为 null)
            var petProxy = LevelManager.Instance.PetProxy;
            if (petProxy?.Inventory != null)
            {
                foreach (var item in petProxy.Inventory)
                {
                    if (item != null)
                        self_items.Add(item.GetInstanceID());
                }
                Log($"Pet inventory items cached: {self_items.Count}");
            }
            else
            {
                Log("WARNING: PetProxy or PetProxy.Inventory is null. Skipping.");
            }

            // 3. 安全读取角色背包 (MainCharacter/CharacterItem 链路很长，极易为 null)
            var mainChar = LevelManager.Instance.MainCharacter;
            if (mainChar?.CharacterItem?.Inventory != null)
            {
                foreach (var item in mainChar.CharacterItem.Inventory)
                {
                    if (item != null)
                        self_items.Add(item.GetInstanceID());
                }
                Log($"Total self items (including char): {self_items.Count}");
            }
            else
            {
                Log("WARNING: MainCharacter or CharacterItem.Inventory is null. Skipping.");
            }

            List<ItemStatsSystem.Item> items = new List<ItemStatsSystem.Item>();
            bool found = false;

            // 4. 安全遍历场景物品
            var allItems = UnityEngine.Object.FindObjectsByType<ItemStatsSystem.Item>(FindObjectsSortMode.None);
            Log($"Total Items found in scene: {allItems.Length}");

            foreach (var item in allItems)
            {
                // 必须检查 item 是否为 null (Unity 对象可能被销毁)
                if (item == null) continue;

                // 1. 检查是否是自己身上的
                // 2. 检查是否来自地上
                // 3. 检查是否有效
                if (self_items.Contains(item.GetInstanceID()) || item.FromInfoKey != "Ground" || item.ActiveAgent == null)
                {
                    continue;
                }

                // 检查 TypeID 是否有效
                int item_type_id = item.TypeID;
                if (targets.ContainsKey(item_type_id))
                {
                    items.Add(item);
                    Log($"Match: {item.DisplayName} ({item_type_id}) Pos: {item.ActiveAgent?.transform?.position}. ID: {item.GetInstanceID()}");

                    if (item_type_id == CROWN_ID && !found)
                    {
                        found = true;
                    }
                }
            }

            Log($"Target items count: {items.Count}");

            if (items.Count == 0)
            {
                StartCoroutine(ShowPoorMessages());
            }
            else
            {
                StartCoroutine(ShowItemsOnGround(items, found));
            }
        }

        private IEnumerator ShowPoorMessages()
        {
            yield return new WaitForSeconds(1.5f);
            CharacterMainControl.Main.PopText("什么都木有!");
            yield return new WaitForSeconds(3f); 
            CharacterMainControl.Main.PopText("什么都木有!!");
            yield return new WaitForSeconds(2f);
            CharacterMainControl.Main.PopText("什么都木有!!!");
        }

        private IEnumerator ShowItemsOnGround(List<ItemStatsSystem.Item> items, bool found)
        {
            yield return new WaitForSeconds(1.5f);
            if (found)
            {
                CharacterMainControl.Main.PopText("找到你了!");
                yield return new WaitForSeconds(2f); // 延迟 3 秒再显示下一个
            }

            foreach (var item in items)
            {
                CharacterMainControl.Main.PopText($"地上有:{item.DisplayName} 坐标({item.ActiveAgent?.transform?.position})");
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
                return;

            // Only draw the circles in the target map
            if (!IsValidRaidScene())
                return;

            CricleState = true;
            // Draw circles
            ClearQuestCircles();

            InteractableItems = UnityEngine.Object.FindObjectsByType<InteractablePickup>(FindObjectsSortMode.None);
            int DrawCount = 0;
            foreach (var item in InteractableItems)
            {
                if(item?.ItemAgent?.Item != null && item.ItemAgent.transform != null)
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
            foreach (var cricle in QuestCircleObjects)
            {
                if (cricle != null && cricle.scene.IsValid())
                {
                    Destroy(cricle);
                }
            }
            QuestCircleObjects.Clear();
            CricleState = false;
        }

        private Sprite GetQuestIcon()
        {
            List<Sprite> AllIcons = MapMarkerManager.Icons;
            if (AllIcons == null)
            {
                Log("无法获取图标。");
                return null;
            }
            if (AllIcons?.Count == null || AllIcons?.Count <= 0)
            {
                Log("图标为空");
            }
            return AllIcons.First();
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
                Log($"异常失败: {e.Message}");
                Destroy(obj);
            }
        }
    }
}