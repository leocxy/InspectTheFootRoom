using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using Duckov.Scenes;
using Duckov.UI;
using ItemStatsSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        // ===== 物品 ID 常量（仅用于首次运行的默认勾选，等价于旧版硬编码表）=====
        private static int
            CROWN_ID = 1254,
            X_KEY = 827,
            O_KEY = 828,
            YELLOW_CARD = 801,
            RED_CARD = 802,
            GREEN_CARD = 803,
            BLUE_CARD = 804,
            BLACK_CARD = 886,
            PURPLE_CARD = 887,
            TANK_BATTERY = 1430,
            CANNONBALL = 1500;

        private static readonly int[] DEFAULT_TARGET_IDS = new int[]
        {
            CROWN_ID, X_KEY, O_KEY,
            YELLOW_CARD, RED_CARD, GREEN_CARD, BLUE_CARD, BLACK_CARD, PURPLE_CARD,
            TANK_BATTERY, CANNONBALL,
        };

        // ===== 玩家可配置的物品选择（替代硬编码 targets）=====
        // 为空 = 扫描地面上全部物品；非空 = 只扫描选中的物品。
        private HashSet<int> _selectedIds = new HashSet<int>(DEFAULT_TARGET_IDS);

        // PlayerPrefs 持久化键
        private const string PREF_IDS = "InspectTheFootRoom.SelectedItems";
        private const string PREF_INIT = "InspectTheFootRoom.SelectionInitialized";

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

        // ===================================================================
        //  配置 UI 相关状态
        // ===================================================================
        private bool _showUI = false;
        private bool _dbLoaded = false;
        private List<ItemInfo> _itemDb = new List<ItemInfo>();
        private string _searchText = "";
        private Vector2 _scrollPos = Vector2.zero;
        private Rect _windowRect = new Rect(40, 40, 380, 520);
        private const int WINDOW_ID = 73512;
        private const int MAX_DRAW = 200;

        private Dictionary<int, Sprite> _iconCache = new Dictionary<int, Sprite>();

        // 物品信息（用于 UI 列表）
        private class ItemInfo
        {
            public int Id;
            public string Name;
            public int Quality;
        }

        void OnEnable()
        {
            Log("Enable");
            LevelManager.OnLevelInitialized += SearchCrownAfterInitialized;
            // 注册画圈事件
            View.OnActiveViewChanged += ToggleQuestCircles;

            // 加载上次保存的选择（无存档则使用默认 10 件）
            LoadSelection();
        }

        void OnDisable()
        {
            Log("Disable");
            LevelManager.OnAfterLevelInitialized -= SearchCrownAfterInitialized;
            // 注销画圈事件
            View.OnActiveViewChanged -= ToggleQuestCircles;
        }

        void Update()
        {
            // F9 切换配置窗口
            if (Input.GetKeyDown(KeyCode.F9))
            {
                _showUI = !_showUI;
                if (_showUI)
                {
                    EnsureDatabase();
                    // 聚焦搜索框
                    GUI.FocusControl("ItemSearch");
                }
            }
        }

        // ===================================================================
        //  OnGUI：物品选择窗口（原生 IMGUI）
        // ===================================================================
        void OnGUI()
        {
            if (!_showUI)
                return;

            _windowRect = GUILayout.Window(WINDOW_ID, _windowRect, DrawConfigWindow,
                "选择要搜寻的物品  (F9 关闭)", GUILayout.Width(380), GUILayout.Height(520));
        }

        private void DrawConfigWindow(int id)
        {
            // 搜索框
            GUILayout.BeginHorizontal();
            GUILayout.Label("搜索:", GUILayout.Width(40));
            GUI.SetNextControlName("ItemSearch");
            _searchText = GUILayout.TextField(_searchText, GUILayout.MinWidth(200));
            GUILayout.EndHorizontal();

            // 统计
            GUILayout.Label($"已选中: {_selectedIds.Count} 个 | 数据库总计: {_itemDb.Count}");

            // 按钮行
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("全选匹配"))
            {
                foreach (var it in FilteredItems(false))
                    _selectedIds.Add(it.Id);
            }
            if (GUILayout.Button("清空选择"))
            {
                _selectedIds.Clear();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("保存配置"))
            {
                SaveSelection();
                // 若小地图当前开着，立即按新选择重绘
                if (MiniMapView.Instance != null && View.ActiveView == MiniMapView.Instance)
                {
                    ClearQuestCircles();
                    DrawQuestCircles();
                }
                CharacterMainControl.Main?.PopText("已保存物品选择");
            }
            if (GUILayout.Button("关闭"))
            {
                _showUI = false;
            }
            GUILayout.EndHorizontal();

            // 列表（滚动 + 勾选）
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));

            int shown = 0;
            foreach (var it in FilteredItems(true))
            {
                bool sel = _selectedIds.Contains(it.Id);
                bool ns = GUILayout.Toggle(sel, $"  {it.Name}  (ID:{it.Id})");
                if (ns != sel)
                {
                    if (ns) _selectedIds.Add(it.Id);
                    else _selectedIds.Remove(it.Id);
                }
                shown++;
            }

            if (shown == 0)
                GUILayout.Label("（无匹配物品）");
            else if (shown >= MAX_DRAW)
                GUILayout.Label($"（仅显示前 {MAX_DRAW} 条，请用搜索过滤）");

            GUILayout.EndScrollView();

            // 允许拖拽窗口
            GUI.DragWindow();
        }

        // 过滤后的物品列表。limit200=true 时最多返回 MAX_DRAW 条（用于绘制）。
        private IEnumerable<ItemInfo> FilteredItems(bool limit200)
        {
            string q = _searchText.Trim().ToLowerInvariant();
            IEnumerable<ItemInfo> list = _itemDb;
            if (!string.IsNullOrEmpty(q))
            {
                list = list.Where(x =>
                    (x.Name != null && x.Name.ToLowerInvariant().Contains(q)) ||
                    x.Id.ToString().Contains(q));
            }
            // 已选中的排在前面，方便查看
            list = list.OrderBy(x => _selectedIds.Contains(x.Id) ? 0 : 1).ThenBy(x => x.Id);
            if (limit200)
                list = list.Take(MAX_DRAW);
            return list;
        }

        // ===================================================================
        //  物品数据库：运行时从 Resources 读取全量物品
        // ===================================================================
        private void EnsureDatabase()
        {
            if (_dbLoaded)
                return;
            _dbLoaded = true;
            _itemDb = new List<ItemInfo>();

            try
            {
                var collection = Resources.Load("ItemAssetsCollection");
                if (collection == null)
                {
                    Log("无法加载 ItemAssetsCollection");
                    CharacterMainControl.Main?.PopText("物品数据库加载失败");
                    return;
                }

                var dicField = collection.GetType().GetField("dic",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (dicField == null)
                {
                    Log("找不到 dic 字段");
                    return;
                }

                var dic = dicField.GetValue(collection) as System.Collections.IDictionary;
                if (dic == null)
                {
                    Log("dic 为 null");
                    return;
                }

                foreach (var key in dic.Keys)
                {
                    var entry = dic[key];
                    if (entry == null) continue;

                    var prefabField = entry.GetType().GetField("prefab",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (prefabField == null) continue;

                    var item = prefabField.GetValue(entry) as ItemStatsSystem.Item;
                    if (item == null) continue;

                    _itemDb.Add(new ItemInfo
                    {
                        Id = item.TypeID,
                        Name = string.IsNullOrEmpty(item.DisplayName) ? ("#" + item.TypeID) : item.DisplayName,
                        Quality = item.Quality,
                    });
                }

                Log($"物品数据库加载完成，共 {_itemDb.Count} 个");
            }
            catch (Exception e)
            {
                Log($"物品数据库加载异常: {e.Message}");
            }
        }

        // ===================================================================
        //  选择的持久化（PlayerPrefs）
        // ===================================================================
        private void LoadSelection()
        {
            if (PlayerPrefs.HasKey(PREF_INIT))
            {
                string raw = PlayerPrefs.GetString(PREF_IDS, "");
                _selectedIds = ParseIds(raw);
                Log($"已从存档加载选择: {_selectedIds.Count} 个物品");
            }
            else
            {
                // 首次运行：默认勾选旧版 10 件高价值物品，保留原有行为
                _selectedIds = new HashSet<int>(DEFAULT_TARGET_IDS);
                Log("首次运行，使用默认物品选择");
            }
        }

        private void SaveSelection()
        {
            PlayerPrefs.SetString(PREF_IDS, string.Join(",", _selectedIds));
            PlayerPrefs.SetInt(PREF_INIT, 1);
            PlayerPrefs.Save();
            Log($"已保存选择: {_selectedIds.Count} 个物品");
        }

        private HashSet<int> ParseIds(string raw)
        {
            var set = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(raw))
                return set;
            foreach (var part in raw.Split(','))
            {
                if (int.TryParse(part.Trim(), out int id))
                    set.Add(id);
            }
            return set;
        }

        // 是否追踪某物品：未选择任何 = 扫全部；否则只扫选中的
        private bool ShouldTrack(int typeId)
        {
            return _selectedIds.Count == 0 || _selectedIds.Contains(typeId);
        }

        // ===================================================================
        //  扫描逻辑
        // ===================================================================
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

                // 检查 TypeID 是否被用户选中（替代旧的硬编码 targets）
                int item_type_id = item.TypeID;
                if (ShouldTrack(item_type_id))
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

        // ===================================================================
        //  小地图画圈
        // ===================================================================
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
                if (item?.ItemAgent?.Item != null && item.ItemAgent.transform != null)
                {
                    var it = item.ItemAgent.Item;
                    if (ShouldTrack(it.TypeID))
                    {
                        DrawCircleMark(it, item.ItemAgent.transform.position, 10f);
                        DrawCount++;
                    }
                }
            }
            Log($"小地图绘制标记: {DrawCount}");
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

        // -------------------------------------------------------------------
        //  物品真实图标加载：反射获取 Item/ItemMetaData 上的图标，转换 Texture2D -> Sprite
        //  找不到时回退到通用地图标记图标（MapMarkerManager.Icons.First()）
        // -------------------------------------------------------------------
        private static readonly string[] ICON_FIELD_NAMES = new string[]
        {
            "Icon", "IconSprite", "Sprite", "ItemIcon", "Image", "Thumbnail", "IconTexture", "IconImage",
        };
        private static readonly string[] META_FIELD_NAMES = new string[]
        {
            "MetaData", "metaData", "ItemMetaData", "itemMetaData",
        };

        private Sprite GetItemIcon(ItemStatsSystem.Item item)
        {
            if (item == null)
                return GetQuestIcon();

            int id = item.TypeID;
            if (_iconCache.TryGetValue(id, out var cached) && cached != null)
                return cached;

            Sprite spr = null;

            // 1) 直接在 Item 上找 Sprite / Texture2D
            spr = FindSprite(item) ?? FindTextureAsSprite(item);

            // 2) 在 Item 的 metaData 上找
            if (spr == null)
            {
                var meta = GetMetaData(item);
                if (meta != null)
                    spr = FindSprite(meta) ?? FindTextureAsSprite(meta);
            }

            if (spr == null)
                spr = GetQuestIcon();

            if (spr != null)
                _iconCache[id] = spr;

            return spr;
        }

        private object GetMetaData(ItemStatsSystem.Item item)
        {
            var t = item.GetType();
            foreach (var name in META_FIELD_NAMES)
            {
                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null)
                {
                    try { return p.GetValue(item, null); } catch { }
                }
                var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null)
                {
                    try { return f.GetValue(item); } catch { }
                }
            }
            return null;
        }

        private Sprite FindSprite(object obj)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            foreach (var name in ICON_FIELD_NAMES)
            {
                // 优先属性
                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(Sprite))
                {
                    try { return p.GetValue(obj, null) as Sprite; } catch { }
                }
                // 再字段
                var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(Sprite))
                {
                    try { return f.GetValue(obj) as Sprite; } catch { }
                }
            }
            return null;
        }

        private Sprite FindTextureAsSprite(object obj)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            foreach (var name in ICON_FIELD_NAMES)
            {
                var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(Texture2D))
                {
                    try
                    {
                        var tex = f.GetValue(obj) as Texture2D;
                        if (tex != null)
                            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }
                    catch { }
                }
                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(Texture2D))
                {
                    try
                    {
                        var tex = p.GetValue(obj, null) as Texture2D;
                        if (tex != null)
                            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }
                    catch { }
                }
            }
            return null;
        }

        private void DrawCircleMark(ItemStatsSystem.Item item, Vector3 position, float radius)
        {
            string itemName = item?.DisplayName ?? "物品";
            GameObject obj = new GameObject($"Item_${itemName}");
            obj.transform.position = position;

            Sprite iconToUse = GetItemIcon(item);

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

            }
            catch (Exception e)
            {
                Log($"异常失败: {e.Message}");
                Destroy(obj);
            }
        }
    }
}
