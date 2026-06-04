using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace Bobbin
{

    [CustomEditor(typeof(BobbinSettings))]
    public class BobbinSettingsEditor : Editor
    {
        [SerializeField] MultiColumnHeaderState m_MultiColumnHeaderState;
        BobbinTreeView m_TreeView;
        SearchField m_SearchField;
        Vector2 logScrollView;
        const string kSessionStateKeyPrefix = "BOB";

        BobbinSettings asset
        {
            get { return (BobbinSettings)target; }
        }

        void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            asset.EnsureValidRoot();

            var treeViewState = new TreeViewState();
            var jsonState = SessionState.GetString(kSessionStateKeyPrefix + asset.GetInstanceID(), "");
            if (!string.IsNullOrEmpty(jsonState))
                JsonUtility.FromJsonOverwrite(jsonState, treeViewState);

            bool firstInit = m_MultiColumnHeaderState == null;
            var headerState = BobbinTreeView.CreateDefaultMultiColumnHeaderState(160);
            if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_MultiColumnHeaderState, headerState))
                MultiColumnHeaderState.OverwriteSerializedFields(m_MultiColumnHeaderState, headerState);
            m_MultiColumnHeaderState = headerState;

            var multiColumnHeader = new BobbinHeader(headerState);
            if (firstInit)
                multiColumnHeader.ResizeToFit();

            var treeModel = new TreeModel<BobbinPath>(asset.paths);
            m_TreeView = new BobbinTreeView(treeViewState, multiColumnHeader, treeModel);
            m_TreeView.treeChanged += OnTreeChanged;
            m_TreeView.beforeDroppingDraggedItems += OnBeforeDroppingDraggedItems;

            m_SearchField = new SearchField();

            m_SearchField.downOrUpArrowKeyPressed += m_TreeView.SetFocusAndEnsureSelectedItem;
        }


        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;

            if (m_TreeView != null)
            {
                m_TreeView.treeChanged -= OnTreeChanged;
                m_TreeView.beforeDroppingDraggedItems -= OnBeforeDroppingDraggedItems;
                SessionState.SetString(kSessionStateKeyPrefix + asset.GetInstanceID(), JsonUtility.ToJson(m_TreeView.state));
            }
        }

        void OnUndoRedoPerformed()
        {
            if (m_TreeView != null)
            {
                m_TreeView.treeModel.SetData(asset.paths);
                m_TreeView.Reload();
            }
        }

        void OnBeforeDroppingDraggedItems(IList<TreeViewItem> draggedRows)
        {
            Undo.RecordObject(asset, string.Format("Moving {0} Item{1}", draggedRows.Count, draggedRows.Count > 1 ? "s" : ""));
        }

        void OnTreeChanged()
        {
            EditorUtility.SetDirty(asset);
        }

        public override void OnInspectorGUI()
        {
            if (asset.autoRefresh || BobbinCore.IsRefreshInProgress)
            {
                Repaint();
            }

            if (m_TreeView == null || m_SearchField == null)
            {
                EditorGUILayout.HelpBox("Bobbin could not initialize the settings table. Select the asset again or recompile the project.", MessageType.Error);
                return;
            }

            EditorGUI.BeginChangeCheck();

            GUILayout.Space(5f);
            ToolBar();
            GUILayout.Space(5f);

            const float topToolbarHeight = 20f;
            const float spacing = 2f;
            float totalHeight = m_TreeView.totalHeight + topToolbarHeight + 2 * spacing;
            Rect rect = GUILayoutUtility.GetRect(0, 10000, 0, totalHeight);
            Rect toolbarRect = new Rect(rect.x, rect.y, rect.width, topToolbarHeight);
            Rect multiColumnTreeViewRect = new Rect(rect.x, rect.y + topToolbarHeight + spacing, rect.width, rect.height - topToolbarHeight - 2 * spacing);
            SearchBar(toolbarRect);
            DoTreeView(multiColumnTreeViewRect);

            GUILayout.Space(5f);
            if (BobbinCore.lastReport != null && BobbinCore.lastReport.Length > 1)
            {
                float reportHeight = EditorStyles.label.CalcHeight(new GUIContent(BobbinCore.lastReport), rect.width);
                logScrollView = EditorGUILayout.BeginScrollView(logScrollView, GUILayout.Height(reportHeight + 20f));
                EditorGUILayout.HelpBox(BobbinCore.lastReport, MessageType.Info);
                EditorGUILayout.EndScrollView();
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(asset);
            }
        }

        void SearchBar(Rect rect)
        {
            m_TreeView.searchString = m_SearchField.OnGUI(rect, m_TreeView.searchString);
        }

        void DoTreeView(Rect rect)
        {
            m_TreeView.OnGUI(rect);
        }

        void ToolBar()
        {

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(BobbinCore.IsRefreshInProgress);
                if (GUILayout.Button(new GUIContent("Refresh", "force Bobbin to scan for changed files immediately"), GUILayout.Width(80)))
                {
                    BobbinCore.DoRefresh();
                }
                EditorGUI.EndDisabledGroup();

                var autoRefresh = EditorGUILayout.ToggleLeft(new GUIContent("Auto refresh?", "if enabled, Bobbin will automatically download new files from the internet"), asset.autoRefresh, GUILayout.MaxWidth(90));
                if (autoRefresh != asset.autoRefresh)
                {
                    RecordSettingsChange("Toggle Bobbin Auto Refresh");
                    asset.autoRefresh = autoRefresh;
                }

                EditorGUILayout.Space();
                GUILayout.Label("every", GUILayout.MaxWidth(36));
                var refreshInterval = System.Convert.ToDouble(Mathf.Clamp(EditorGUILayout.IntField(System.Convert.ToInt32(asset.refreshInterval), GUILayout.MaxWidth(30)), 5, 999));
                if (!refreshInterval.Equals(asset.refreshInterval))
                {
                    RecordSettingsChange("Edit Bobbin Refresh Interval");
                    asset.refreshInterval = refreshInterval;
                }
                GUILayout.Label("sec.", GUILayout.MaxWidth(30));

                GUILayout.Label("timeout", GUILayout.MaxWidth(48));
                var timeoutSeconds = Mathf.Clamp(EditorGUILayout.IntField(asset.requestTimeoutSeconds <= 0 ? 30 : asset.requestTimeoutSeconds, GUILayout.MaxWidth(38)), 1, 600);
                if (timeoutSeconds != asset.requestTimeoutSeconds)
                {
                    RecordSettingsChange("Edit Bobbin Request Timeout");
                    asset.requestTimeoutSeconds = timeoutSeconds;
                }
                GUILayout.Label("sec.", GUILayout.MaxWidth(30));

                GUILayout.EndHorizontal();
                GUILayout.Space(5);
                if (BobbinCore.IsRefreshInProgress)
                {
                    Rect rect = GUILayoutUtility.GetRect(50, 18, "TextField", GUILayout.MaxWidth(10000));
                    EditorGUI.ProgressBar(rect, 1f, "Refresh running...");
                }
                else if (asset.autoRefresh)
                {
                    Rect rect = GUILayoutUtility.GetRect(50, 18, "TextField", GUILayout.MaxWidth(10000));
                    var progress = EditorApplication.timeSinceStartup - BobbinCore.lastRefreshTime;
                    var interval = System.Math.Max(5.0, asset.refreshInterval);
                    var progressPercent = progress / interval;
                    var remainingSeconds = System.Math.Max(0.0, interval - progress);
                    EditorGUI.ProgressBar(rect, Mathf.Clamp01(System.Convert.ToSingle(progressPercent)), "Auto refresh in " + remainingSeconds.ToString("F0"));
                }
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var style = "miniButton";

                if (GUILayout.Button("Add New File", style))
                {
                    Undo.RecordObject(asset, "Add Item To Asset");

                    TreeElement parent = m_TreeView.treeModel.root;
                    int depth = parent != null ? parent.depth + 1 : 0;
                    int id = m_TreeView.treeModel.GenerateUniqueID();
                    var element = new BobbinPath("Item " + id, depth, id);
                    m_TreeView.treeModel.AddElement(element, parent, 0);
                    EditorUtility.SetDirty(asset);

                    // Select newly created element
                    m_TreeView.SetSelection(new[] { id }, TreeViewSelectionOptions.RevealAndFrame);
                }

                var selection = m_TreeView.GetSelection();
                EditorGUI.BeginDisabledGroup(selection.Count == 0);
                if (GUILayout.Button("Remove Highlighted File(s)", style))
                {
                    Undo.RecordObject(asset, "Remove Item From Asset");
                    m_TreeView.treeModel.RemoveElements(selection);
                    EditorUtility.SetDirty(asset);
                }
                EditorGUI.EndDisabledGroup();
            }
        }

        void RecordSettingsChange(string undoName)
        {
            Undo.RecordObject(asset, undoName);
            EditorUtility.SetDirty(asset);
        }

        internal class BobbinHeader : MultiColumnHeader
        {
            Mode m_Mode;

            public enum Mode
            {
                LargeHeader,
                DefaultHeader,
                MinimumHeaderWithoutSorting
            }

            public BobbinHeader(MultiColumnHeaderState state)
                : base(state)
            {
                mode = Mode.DefaultHeader;
            }

            public Mode mode
            {
                get
                {
                    return m_Mode;
                }
                set
                {
                    m_Mode = value;
                    switch (m_Mode)
                    {
                        case Mode.LargeHeader:
                            canSort = true;
                            height = 37f;
                            break;
                        case Mode.DefaultHeader:
                            canSort = true;
                            height = DefaultGUI.defaultHeight;
                            break;
                        case Mode.MinimumHeaderWithoutSorting:
                            canSort = false;
                            height = DefaultGUI.minimumHeight;
                            break;
                    }
                }
            }

            protected override void ColumnHeaderGUI(MultiColumnHeaderState.Column column, Rect headerRect, int columnIndex)
            {
                // Default column header gui
                base.ColumnHeaderGUI(column, headerRect, columnIndex);
            }
        }

    }
}

