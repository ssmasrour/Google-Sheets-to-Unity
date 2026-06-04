using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;

namespace Bobbin
{
    internal class BobbinTreeView : TreeViewWithTreeModel<BobbinPath>
    {
        const float kRowHeights = 20f;
        const float kToggleWidth = 18f;

        // All columns
        enum Columns
        {
            Enabled,
            Name,
            SourceUrl,
            SheetGid,
            FileType,
            Asset,
        }

        public enum SortOption
        {
            Enabled,
            Name,
            SourceUrl,
            SheetGid,
            FileType,
            Asset,
        }

        // Sort options per column
        SortOption[] m_SortOptions =
        {
            SortOption.Enabled,
            SortOption.Name,
            SortOption.SourceUrl,
            SortOption.SheetGid,
            SortOption.FileType,
            SortOption.Asset,
        };

        public static void TreeToList(TreeViewItem root, IList<TreeViewItem> result)
        {
            if (root == null)
                throw new ArgumentNullException("root");
            if (result == null)
                throw new ArgumentNullException("result");

            result.Clear();

            if (root.children == null)
                return;

            Stack<TreeViewItem> stack = new Stack<TreeViewItem>();
            for (int i = root.children.Count - 1; i >= 0; i--)
                stack.Push(root.children[i]);

            while (stack.Count > 0)
            {
                TreeViewItem current = stack.Pop();
                result.Add(current);

                if (current.hasChildren)
                {
                    for (int i = current.children.Count - 1; i >= 0; i--)
                    {
                        if (current.children[i] != null)
                        {
                            stack.Push(current.children[i]);
                        }
                    }
                }
            }
        }

        public BobbinTreeView(TreeViewState state, MultiColumnHeader multicolumnHeader, TreeModel<BobbinPath> model) : base(state, multicolumnHeader, model)
        {
            Assert.AreEqual(m_SortOptions.Length, Enum.GetValues(typeof(Columns)).Length, "Ensure number of sort options are in sync with number of Columns enum values");

            // Custom setup
            rowHeight = kRowHeights;
            columnIndexForTreeFoldouts = 2;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            customFoldoutYOffset = (kRowHeights - EditorGUIUtility.singleLineHeight) * 0.5f; // center foldout in the row since we also center content. See RowGUI
            extraSpaceBeforeIconAndLabel = kToggleWidth;
            multicolumnHeader.sortingChanged += OnSortingChanged;

            Reload();
        }


        // Only visible rows are built; the model keeps the full tree information.
        // The treeview only creates info for the row list.
        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            var rows = base.BuildRows(root);
            SortIfNeeded(root, rows);
            return rows;
        }

        void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        {
            SortIfNeeded(rootItem, GetRows());
        }

        void SortIfNeeded(TreeViewItem root, IList<TreeViewItem> rows)
        {
            if (rows.Count <= 1)
                return;

            if (multiColumnHeader.sortedColumnIndex == -1)
            {
                return; // No column to sort for (just use the order the data are in)
            }

            // Sort the roots of the existing tree items
            SortByMultipleColumns();
            TreeToList(root, rows);
            Repaint();
        }

        void SortByMultipleColumns()
        {
            var sortedColumns = multiColumnHeader.state.sortedColumns;

            if (sortedColumns.Length == 0)
                return;

            var myTypes = rootItem.children.Cast<TreeViewItem<BobbinPath>>();
            var orderedQuery = InitialOrder(myTypes, sortedColumns);
            for (int i = 1; i < sortedColumns.Length; i++)
            {
                SortOption sortOption = m_SortOptions[sortedColumns[i]];
                bool ascending = multiColumnHeader.IsSortedAscending(sortedColumns[i]);

                switch (sortOption)
                {
                    case SortOption.Enabled:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.enabled, ascending);
                        break;
                    case SortOption.Name:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.name, ascending);
                        break;
                    case SortOption.SourceUrl:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.GetSourceUrl(), ascending);
                        break;
                    case SortOption.SheetGid:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.sheetId, ascending);
                        break;
                    case SortOption.FileType:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.fileType, ascending);
                        break;
                    case SortOption.Asset:
                        orderedQuery = orderedQuery.ThenBy(l => GetAssetName(l.data), ascending);
                        break;
                }
            }

            rootItem.children = orderedQuery.Cast<TreeViewItem>().ToList();
        }

        IOrderedEnumerable<TreeViewItem<BobbinPath>> InitialOrder(IEnumerable<TreeViewItem<BobbinPath>> myTypes, int[] history)
        {
            SortOption sortOption = m_SortOptions[history[0]];
            bool ascending = multiColumnHeader.IsSortedAscending(history[0]);
            switch (sortOption)
            {
                case SortOption.Enabled:
                    return myTypes.Order(l => l.data.enabled, ascending);
                case SortOption.Name:
                    return myTypes.Order(l => l.data.name, ascending);
                case SortOption.SourceUrl:
                    return myTypes.Order(l => l.data.GetSourceUrl(), ascending);
                case SortOption.SheetGid:
                    return myTypes.Order(l => l.data.sheetId, ascending);
                case SortOption.FileType:
                    return myTypes.Order(l => l.data.fileType, ascending);
                case SortOption.Asset:
                    return myTypes.Order(l => GetAssetName(l.data), ascending);
                default:
                    Assert.IsTrue(false, "Unhandled enum");
                    break;
            }

            // default
            return myTypes.Order(l => l.data.name, ascending);
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = (TreeViewItem<BobbinPath>)args.item;

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                CellGUI(args.GetCellRect(i), item, (Columns)args.GetColumn(i), ref args);
            }
        }

        void CellGUI(Rect cellRect, TreeViewItem<BobbinPath> item, Columns column, ref RowGUIArgs args)
        {
            // Center cell rect vertically (makes it easier to place controls, icons etc in the cells)
            CenterRectUsingSingleLineHeight(ref cellRect);

            switch (column)
            {
                case Columns.Enabled:
                    {
                        var enabled = EditorGUI.Toggle(cellRect, new GUIContent("", "enable refresh?"), item.data.enabled);
                        if (enabled != item.data.enabled)
                        {
                            RecordSettingsChange("Toggle Bobbin File");
                            item.data.enabled = enabled;
                        }
                    }
                    break;

                case Columns.Name:
                    {
                        cellRect.width -= 20;
                        var name = GUI.TextField(cellRect, item.data.name ?? string.Empty);
                        if (name != item.data.name)
                        {
                            RecordSettingsChange("Rename Bobbin File");
                            item.data.name = name;
                        }

                        cellRect.x += cellRect.width;
                        cellRect.width = 20;
                        var previousBackgroundColor = GUI.backgroundColor;
                        GUI.backgroundColor = Color.Lerp(Color.red, Color.white, 0.75f);
                        if (GUI.Button(cellRect, new GUIContent("x", "delete this item")))
                        {
                            if (EditorUtility.DisplayDialog("Bobbin: confirm deletion", "Really delete " + item.data.name + "?", "Yes, delete", "No, cancel"))
                            {
                                RecordSettingsChange("Remove Bobbin File");
                                var list = new List<BobbinPath> { item.data };
                                treeModel.RemoveElements(list);
                            }
                        }

                        GUI.backgroundColor = previousBackgroundColor;
                    }
                    break;

                case Columns.SourceUrl:
                    {
                        cellRect.xMin += 5f;
                        var sourceUrl = BobbinCore.UnfixURL(item.data.GetSourceUrl());
                        var hasUrl = sourceUrl.Length > 4;
                        var urlRect = cellRect;
                        if (hasUrl)
                        {
                            urlRect.width -= 22;
                        }

                        var newUrl = GUI.TextField(urlRect, sourceUrl);
                        if (newUrl != sourceUrl)
                        {
                            RecordSettingsChange("Edit Bobbin URL");
                            item.data.SetSourceUrl(newUrl);
                        }

                        if (hasUrl)
                        {
                            var openRect = new Rect(urlRect.xMax + 2, cellRect.y, 20, cellRect.height);
                            if (GUI.Button(openRect, new GUIContent(">", "open source URL in web browser")))
                            {
                                Application.OpenURL(BobbinCore.UnfixURL(item.data.GetSourceUrl()));
                            }
                        }
                    }
                    break;

                case Columns.SheetGid:
                    {
                        cellRect.xMin += 5f;
                        var sheetId = GUI.TextField(cellRect, item.data.sheetId ?? string.Empty);
                        if (sheetId != item.data.sheetId)
                        {
                            RecordSettingsChange("Edit Bobbin Sheet GID");
                            item.data.sheetId = sheetId;
                        }
                    }
                    break;

                case Columns.FileType:
                    {
                        cellRect.xMin += 5f;
                        var fileType = (FileType)EditorGUI.EnumPopup(cellRect, item.data.fileType);
                        if (fileType != item.data.fileType)
                        {
                            RecordSettingsChange("Edit Bobbin File Type");
                            item.data.fileType = fileType;
                        }
                    }
                    break;

                case Columns.Asset:
                    {
                        cellRect.xMin += 5f;
                        if (item.data.assetReference != null)
                        {
                            var resetRect = new Rect(cellRect.x, cellRect.y, 20, cellRect.height);
                            var objectRect = new Rect(cellRect.x + 24, cellRect.y, Mathf.Max(0, cellRect.width - 24), cellRect.height);

                            EditorGUI.BeginDisabledGroup(true);
                            EditorGUI.ObjectField(objectRect, item.data.assetReference, typeof(UnityEngine.Object), false);
                            EditorGUI.EndDisabledGroup();

                            if (GUI.Button(resetRect, new GUIContent("x", "reset asset file path\n" + item.data.filePath)))
                            {
                                RecordSettingsChange("Clear Bobbin Asset Path");
                                item.data.assetReference = null;
                                item.data.filePath = string.Empty;
                                item.data.lastFileHash = string.Empty;
                            }
                        }
                        else
                        {
                            if (GUI.Button(cellRect, new GUIContent("Save As...", "select the asset file path")))
                            {
                                var defaultName = SanitizeFileName(string.IsNullOrEmpty(item.data.name) ? "BobbinFile" : item.data.name);
                                var extension = item.data.fileType.ToString();
                                var newPath = EditorUtility.SaveFilePanelInProject(
                                    "Bobbin: save " + item.data.name + " URL as file...",
                                    defaultName + "." + extension,
                                    extension,
                                    "Save URL as file...");

                                if (!string.IsNullOrEmpty(newPath))
                                {
                                    RecordSettingsChange("Set Bobbin Asset Path");
                                    item.data.filePath = newPath;
                                    item.data.lastFileHash = string.Empty;
                                    if (item.data.GetSourceUrl().Length > 4)
                                    {
                                        BobbinCore.DoRefresh();
                                    }
                                }
                            }
                        }
                    }
                    break;
            }
        }

        static void RecordSettingsChange(string undoName)
        {
            Undo.RecordObject(BobbinSettings.Instance, undoName);
            EditorUtility.SetDirty(BobbinSettings.Instance);
        }

        static string GetAssetName(BobbinPath path)
        {
            return path != null && path.assetReference != null ? path.assetReference.name : string.Empty;
        }

        static string SanitizeFileName(string value)
        {
            var invalidCharacters = Path.GetInvalidFileNameChars();
            var fileName = value;
            for (int i = 0; i < invalidCharacters.Length; i++)
            {
                fileName = fileName.Replace(invalidCharacters[i], '_');
            }

            return string.IsNullOrEmpty(fileName) ? "BobbinFile" : fileName;
        }

        // Rename
        //--------

        protected override bool CanRename(TreeViewItem item)
        {
            // Only allow rename if we can show the rename overlay with a certain width (label might be clipped by other columns)
            //Rect renameRect = GetRenameRect (treeViewRect, 0, item);
            //return renameRect.width > 30;
            return false;
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            // Set the backend name and reload the tree to reflect the new model
            if (args.acceptedRename)
            {
                var element = treeModel.Find(args.itemID);
                if (element != null)
                {
                    element.name = args.newName;
                    Reload();
                }
            }
        }

        protected override Rect GetRenameRect(Rect rowRect, int row, TreeViewItem item)
        {
            Rect cellRect = GetCellRectForTreeFoldouts(rowRect);
            CenterRectUsingSingleLineHeight(ref cellRect);
            return base.GetRenameRect(cellRect, row, item);
        }

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            return false;
        }

        // Misc
        //--------

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return true;
        }

        public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState(float treeViewWidth)
        {
            var columns = new[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("▶", "Enable or disable refreshing for each file"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Center,
                    width = 24,
                    minWidth = 24,
                    autoResize = false,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Name", "Write a descriptive note or label here (like 'Dialogue1' or 'FinalStats' or 'Level3')"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 110,
                    minWidth = 60,
                    autoResize = true,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("URL", "Bobbin will try to fetch and download content at this URL."),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 110,
                    minWidth = 60,
                    autoResize = true,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("GID", "Optional Google Sheets gid. Leave blank for the first sheet."),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 40,
                    minWidth = 20,
                    autoResize = true,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Type", "if you need more file extensions, edit the FileType enum in BobbinSettings.cs"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 48,
                    minWidth = 40,
                    autoResize = true,
                    allowToggleVisibility = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Asset", "The generated asset file as imported by Unity."),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 70,
                    minWidth = 40,
                    autoResize = true,
                    allowToggleVisibility = true
                }
            };

            Assert.AreEqual(columns.Length, Enum.GetValues(typeof(Columns)).Length, "Number of columns should match number of enum values: You probably forgot to update one of them.");

            var state = new MultiColumnHeaderState(columns);
            return state;
        }
    }

    static class EnumerableSortExtensions
    {
        public static IOrderedEnumerable<T> Order<T, TKey>(this IEnumerable<T> source, Func<T, TKey> selector, bool ascending)
        {
            if (ascending)
            {
                return source.OrderBy(selector);
            }
            else
            {
                return source.OrderByDescending(selector);
            }
        }

        public static IOrderedEnumerable<T> ThenBy<T, TKey>(this IOrderedEnumerable<T> source, Func<T, TKey> selector, bool ascending)
        {
            if (ascending)
            {
                return source.ThenBy(selector);
            }
            else
            {
                return source.ThenByDescending(selector);
            }
        }
    }
}
