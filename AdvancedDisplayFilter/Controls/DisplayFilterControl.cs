﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Sdl.Community.AdvancedDisplayFilter.DisplayFilters;
using Sdl.Community.AdvancedDisplayFilter.Helpers;
using Sdl.Community.AdvancedDisplayFilter.Models;
using Sdl.Community.AdvancedDisplayFilter.Services;
using Sdl.Community.Toolkit.FileType;
using Sdl.FileTypeSupport.Framework.NativeApi;
using Sdl.TranslationStudioAutomation.IntegrationApi;

namespace Sdl.Community.AdvancedDisplayFilter.Controls
{
	public partial class DisplayFilterControl : UserControl
	{
		public delegate void OnApplyFilterHandler(DisplayFilterSettings displayFilterSettings, CustomFilterSettings customSettings, bool reverse, FilteredCountsCallback result);
		public event OnApplyFilterHandler OnApplyDisplayFilter;
		public delegate void FilteredCountsCallback(int filteredSegments, int totalSegments);

		private readonly HighlightService _highlightService;
		private CustomFilterSettings _customFilterSettings;		
		private Document _activeDocument;
		private bool _uniqueSegments;
		private bool _editedFuzzy;
		private bool _unEditedFuzzy;
		private bool _reverseFilter;
		private bool _MT;

		public DisplayFilterControl()
		{
			InitializeComponent();

			AddGroupsToOriginTypeListview();

			InitializeSettings();

			_highlightService = new HighlightService();

			// colorsListView.View = View.List;
			colorsListView.CheckBoxes = false;
			colorsListView.View = View.Tile;
			colorsListView.TileSize = new Size(70, 20);

			listView_available.ListViewItemSorter = new ListViewItemComparer();
			listView_selected.ListViewItemSorter = new ListViewItemComparer();

			var editorController = GetEditorController();
			editorController.ActiveDocumentChanged += EditorController_ActiveDocumentChanged;

			_activeDocument = editorController.ActiveDocument;
			ActiveDocumentChanged(_activeDocument);

			OnApplyDisplayFilter += ApplyDisplayFilter;

			listView_available.SetGroupState(ListViewGroupState.Collapsible | ListViewGroupState.Collapsed);
			listView_selected.SetGroupState(ListViewGroupState.Collapsible | ListViewGroupState.Normal);

			listView_available.SetGroupState(ListViewGroupState.Collapsible | ListViewGroupState.Normal, listView_available.Groups[1]);
			listView_available.SetGroupState(ListViewGroupState.Collapsible | ListViewGroupState.Normal, listView_available.Groups[2]);

			segmentsBox.Enabled = false;

			content_toolTips.SetToolTip(label_dsiLocation, StringResources.Tooltip_Document_Structure_Information_Location);

			SetupHighlightMenu();
		}		

		#region  |  ListView Groups  |

		internal static ListViewGroup GroupStatusAvailable { get; set; }
		internal static ListViewGroup GroupOriginAvailable { get; set; }
		internal static ListViewGroup GroupPreviousOriginAvailable { get; set; }
		internal static ListViewGroup GroupGeneralAvailable { get; set; }
		internal static ListViewGroup GroupRepetitionTypesAvailable { get; set; }
		internal static ListViewGroup GroupReviewTypesAvailable { get; set; }
		internal static ListViewGroup GroupLockingTypesAvailable { get; set; }
		internal static ListViewGroup GroupContentTypesAvailable { get; set; }

		private static ListViewGroup GroupStatusSelected { get; set; }
		private static ListViewGroup GroupOriginSelected { get; set; }
		private static ListViewGroup GroupPreviousOriginSelected { get; set; }
		private static ListViewGroup GroupGeneralSelected { get; set; }
		private static ListViewGroup GroupRepetitionTypesSelected { get; set; }
		private static ListViewGroup GroupReviewTypesSelected { get; set; }
		private static ListViewGroup GroupLockingTypesSelected { get; set; }
		private static ListViewGroup GroupContentTypesSelected { get; set; }

		#endregion
		
		public DisplayFilter DisplayFilter { get; set; }

		public IList<IContextInfo> ContextInfoList { get; set; }

		public List<string> AvailableColorsList { get; set; }		
		
		private CustomFilterSettings CustomFilterFilterSettings
		{
			get
			{
				_customFilterSettings = new CustomFilterSettings
				{
					SourceAndTargetLogicalOperator =
						comboBox_SourceTargetFilterLogicalOperator.SelectedIndex == 0
							? CustomFilterSettings.LogicalOperators.And
							: CustomFilterSettings.LogicalOperators.Or,
					UseBackreferences = checkBox_useBackReferences.Checked,
					OddsNo = oddBtn.Checked,
					EvenNo = evenBtn.Checked,
					Grouped = groupedBtn.Checked,
					None = noneBtn.Checked,
					UseRegexCommentSearch = commentRegexBox.Checked,
					Colors = new List<string>(),
					FuzzyMin = fuzzyMin.Text,
					FuzzyMax = fuzzyMax.Text,
					SplitSegments = splitCheckBox.Checked,
					MergedSegments = mergedCheckbox.Checked,
					SourceEqualsTarget = sourceSameBox.Checked,
					IsEqualsCaseSensitive = equalsCaseSensitive.Checked,
					Unique = _uniqueSegments,
					MergedAcross = mergedAcross.Checked,
					ContainsTags = containsTagsCheckBox.Checked,
					ModifiedBy = modifiedByBox.Text,
					ModifiedByChecked = modifiedByCheck.Checked,
					CreatedBy = createdByBox.Text,
					CreatedByChecked = createdByCheck.Checked,
					EditedFuzzy = _editedFuzzy,
					UnEditedFuzzy = _unEditedFuzzy,
					ContextInfoStringId = stringId_textbox.Text,
          UseTagContent = checkBox_TagContent.Checked,
          AndOrTagContent = alsoTags_radioButton.Checked,
					MT = _MT
				};

				foreach (ListViewItem color in colorsListView.SelectedItems)
				{
					var colorCode = color.Text;

					if (!_customFilterSettings.Colors.Contains(colorCode))
					{
						_customFilterSettings.Colors.Add(colorCode);
					}
				}

				if (groupedBtn.Checked)
				{
					_customFilterSettings.GroupedList = segmentsBox.Text;
				}

				if (commentRegexBox.Checked)
				{
					_customFilterSettings.CommentRegex = textBox_commentText.Text;
				}

				return _customFilterSettings;
			}
			set
			{
				if (value == null)
				{
					return;
				}

				//segments settings 
				comboBox_SourceTargetFilterLogicalOperator.SelectedIndex =
					value.SourceAndTargetLogicalOperator == CustomFilterSettings.LogicalOperators.And ? 0 : 1;
				checkBox_useBackReferences.Checked = value.UseBackreferences;
				oddBtn.Checked = value.OddsNo;
				evenBtn.Checked = value.EvenNo;
				noneBtn.Checked = value.None;
				groupedBtn.Checked = value.Grouped;
				splitCheckBox.Checked = value.SplitSegments;
				mergedCheckbox.Checked = value.MergedSegments;
				fuzzyMin.Text = value.FuzzyMin;
				fuzzyMax.Text = value.FuzzyMax;
				sourceSameBox.Checked = value.SourceEqualsTarget;
				equalsCaseSensitive.Checked = value.IsEqualsCaseSensitive;
				_uniqueSegments = value.Unique;
				commentRegexBox.Checked = value.UseRegexCommentSearch;
				_customFilterSettings.Colors = value.Colors;
				mergedAcross.Checked = value.MergedAcross;
				containsTagsCheckBox.Checked = value.ContainsTags;
				modifiedByBox.Text = value.ModifiedBy;
				modifiedByCheck.Checked = value.ModifiedByChecked;
				createdByBox.Text = value.CreatedBy;
				createdByCheck.Checked = value.CreatedByChecked;
				dsiLocation_textbox.Text = value.ContextInfoStringId;

				foreach (var color in value.Colors)
				{
					foreach (ListViewItem colorItem in colorsListView.Items)
					{
						if (colorItem.Text.Equals(color))
						{
							colorItem.Selected = true;
						}
					}
				}

				if (groupedBtn.Checked)
				{
					segmentsBox.Text = value.GroupedList;
				}
			}
		}

		private DisplayFilterSettings DisplayFilterSettings
		{
			get
			{
				#region  |  get settings  |

				var settings = new DisplayFilterSettings
				{
					IsRegularExpression = checkBox_regularExpression.Checked,
					IsCaseSensitive = checkBox_caseSensitive.Checked,
					SourceText = textBox_source.Text,
					TargetText = target_textbox.Text,
					CommentText = textBox_commentText.Text,
					CommentAuthor = textBox_commentAuthor.Text,
					CommentSeverity = comboBox_commentSeverity.SelectedIndex,
					ShowAllContent = false
				};

				foreach (var contextInfo in listView_contextInfo.SelectedItems
					.Cast<ListViewItem>().Select(selectedItem => selectedItem.Tag as IContextInfo)
					.Where(contextInfo => contextInfo != null
						&& !settings.ContextInfoTypes.Contains(contextInfo.ContextType)))
				{
					settings.ContextInfoTypes.Add(contextInfo.ContextType);
				}

				foreach (ListViewItem item in listView_selected.Items)
				{
					if (item.Group == GroupGeneralSelected
						&& item.Tag.ToString() == StringResources.DisplayFilterControl_ShowAllContent)
						settings.ShowAllContent = true;
					else if (item.Group == GroupOriginSelected)
						settings.OriginTypes.Add(item.Tag.ToString());
					else if (item.Group == GroupPreviousOriginSelected)
						settings.PreviousOriginTypes.Add(item.Tag.ToString());
					else if (item.Group == GroupStatusSelected)
						settings.ConfirmationLevels.Add(item.Tag.ToString());
					else if (item.Group == GroupRepetitionTypesSelected)
						settings.RepetitionTypes.Add(item.Tag.ToString());
					else if (item.Group == GroupReviewTypesSelected)
						settings.SegmentReviewTypes.Add(item.Tag.ToString());
					else if (item.Group == GroupLockingTypesSelected)
						settings.SegmentLockingTypes.Add(item.Tag.ToString());
					else if (item.Group == GroupContentTypesSelected)
						settings.SegmentContentTypes.Add(item.Tag.ToString());
				}

				#endregion
				return settings;
			}
			set
			{
				if (value == null)
				{
					return;
				}

				#region  |  set settings  |

				#region  |  content panel  |

				textBox_source.Text = value.SourceText;
				target_textbox.Text = value.TargetText;

				checkBox_regularExpression.Checked = value.IsRegularExpression;
				checkBox_caseSensitive.Checked = value.IsCaseSensitive;

				#endregion

				#region  |  filters panel  |

				try
				{
					listView_available.BeginUpdate();
					listView_selected.BeginUpdate();

					MoveAllListViewItems(listView_selected, listView_available);

					foreach (var item in listView_available.Items.Cast<ListViewItem>()
						.Where(
							item =>
								item.Group == GroupGeneralAvailable
								&&
								item.Tag.ToString() ==
								StringResources.DisplayFilterControl_ShowAllContent
								&& value.ShowAllContent))
					{

						MoveListViewItem(listView_available, item, listView_selected);
					}


					if (value.RepetitionTypes.Any())
					{
						foreach (var item in listView_available.Items.Cast<ListViewItem>()
							.Where(
								item =>
									item.Group == GroupRepetitionTypesAvailable &&
									value.RepetitionTypes.Contains(item.Tag.ToString())))
						{

							MoveListViewItem(listView_available, item, listView_selected);
						}
					}

					if (value.SegmentReviewTypes.Any())
					{
						foreach (var item in listView_available.Items.Cast<ListViewItem>()
							.Where(
								item =>
									item.Group == GroupReviewTypesAvailable &&
									value.SegmentReviewTypes.Contains(item.Tag.ToString())))
						{

							MoveListViewItem(listView_available, item, listView_selected);
						}
					}

					if (value.SegmentLockingTypes.Any())
					{
						foreach (var item in listView_available.Items.Cast<ListViewItem>()
							.Where(
								item =>
									item.Group == GroupLockingTypesAvailable &&
									value.SegmentLockingTypes.Contains(item.Tag.ToString())))
						{
							MoveListViewItem(listView_available, item, listView_selected);
						}
					}

					if (value.SegmentContentTypes.Any())
					{
						foreach (var item in listView_available.Items.Cast<ListViewItem>()
							.Where(
								item =>
									item.Group == GroupContentTypesAvailable &&
									value.SegmentContentTypes.Contains(item.Tag.ToString())))
						{
							MoveListViewItem(listView_available, item, listView_selected);
						}
					}


					if (value.ConfirmationLevels.Any())
					{
						foreach (var item in listView_available.Items.Cast<ListViewItem>()
							.Where(
								item =>
									item.Group == GroupStatusAvailable &&
									value.ConfirmationLevels.Contains(item.Tag.ToString())))
						{
							MoveListViewItem(listView_available, item, listView_selected);
						}
					}


					if (value.OriginTypes.Any())
					{
						foreach (var item in listView_available.Items.Cast<ListViewItem>()
							.Where(
								item =>
									item.Group == GroupOriginAvailable &&
									value.OriginTypes.Contains(item.Tag.ToString())))
						{
							MoveListViewItem(listView_available, item, listView_selected);
						}
					}

					if (value.PreviousOriginTypes.Any())
					{
						foreach (var item in listView_available.Items.Cast<ListViewItem>()
							.Where(
								item =>
									item.Group == GroupPreviousOriginAvailable &&
									value.PreviousOriginTypes.Contains(item.Tag.ToString())))
						{
							MoveListViewItem(listView_available, item, listView_selected);
						}
					}
				}
				finally
				{
					listView_available.EndUpdate();
					listView_selected.EndUpdate();
				}

				#endregion

				#region  |  comments panel  |

				textBox_commentText.Text = value.CommentText;
				textBox_commentAuthor.Text = value.CommentAuthor;
				comboBox_commentSeverity.SelectedIndex = value.CommentSeverity;


				#endregion

				#region  |  context info panel  |


				foreach (ListViewItem item in listView_contextInfo.Items)
				{
					var contextInfoItem = item.Tag as IContextInfo;
					if (contextInfoItem != null && value.ContextInfoTypes.Contains(contextInfoItem.ContextType))
						item.Selected = true;
					else
						item.Selected = false;
				}

				#endregion

				#endregion
			}
		}

		private void InitializeSettings()
		{
			#region segments number

			evenBtn.Checked = false;
			oddBtn.Checked = false;
			groupedBtn.Checked = false;
			segmentsBox.Text = string.Empty;
			segmentsBox.Enabled = false;
			fuzzyMin.Text = string.Empty;
			fuzzyMax.Text = string.Empty;
			splitCheckBox.Checked = false;
			mergedCheckbox.Checked = false;
			mergedAcross.Checked = false;
			commentRegexBox.Checked = false;
			sourceSameBox.Checked = false;
			equalsCaseSensitive.Checked = false;
			_uniqueSegments = false;
			colorsListView.SelectedItems.Clear();
			_reverseFilter = false;
			containsTagsCheckBox.Checked = false;
			modifiedByBox.Text = string.Empty;
			modifiedByCheck.Checked = false;
			createdByBox.Text = string.Empty;
			createdByCheck.Checked = false;
			_unEditedFuzzy = false;
			_editedFuzzy = false;
			_MT = false;
			stringId_textbox.Text = string.Empty;
			#endregion


			#region  |  content panel  |

			textBox_source.Text = string.Empty;
			target_textbox.Text = string.Empty;

			checkBox_regularExpression.Checked = false;
			checkBox_caseSensitive.Checked = false;
			checkBox_TagContent.Checked = false;

			alsoTags_radioButton.Checked = true;

			comboBox_SourceTargetFilterLogicalOperator.SelectedIndex = 0;

			CheckBox_regularExpression_CheckedChanged(checkBox_regularExpression, null);
			CheckBox_useBackReferences_CheckedChanged(checkBox_useBackReferences, null);

			#endregion

			#region  |  filters panel  |

			try
			{
				listView_available.BeginUpdate();
				listView_selected.BeginUpdate();

				listView_available.Items.Clear();
				listView_selected.Items.Clear();

				var viewItem = listView_available.Items.Add(StringResources.DisplayFilterControl_Show_All_Content);
				viewItem.Group = GroupGeneralAvailable;
				viewItem.Tag = StringResources.DisplayFilterControl_ShowAllContent;

				foreach (var type in Enum.GetValues(typeof(DisplayFilterSettings.RepetitionType)))
				{
					var item = listView_available.Items.Add(Helper.GetTypeName((DisplayFilterSettings.RepetitionType)type));
					item.Group = GroupRepetitionTypesAvailable;
					item.Tag = type;
				}

				//unique 
				var unique = listView_available.Items.Add("Unique Occurrences");
				unique.Group = GroupRepetitionTypesAvailable;
				unique.Tag = "Unique";

				var mt = listView_available.Items.Add("Machine Translated");
				mt.Group = GroupGeneralAvailable;
				mt.Tag = "MT";

				foreach (var type in Enum.GetValues(typeof(DisplayFilterSettings.SegmentReviewType)))
				{
					var item =
						listView_available.Items.Add(Helper.GetTypeName((DisplayFilterSettings.SegmentReviewType)type));

					item.Group = GroupReviewTypesAvailable;
					item.Tag = type;
				}

				foreach (var type in Enum.GetValues(typeof(DisplayFilterSettings.SegmentLockingType)))
				{
					var item =
						listView_available.Items.Add(Helper.GetTypeName((DisplayFilterSettings.SegmentLockingType)type));

					item.Group = GroupLockingTypesAvailable;
					item.Tag = type;
				}

				foreach (var type in Enum.GetValues(typeof(DisplayFilterSettings.SegmentContentType)))
				{
					var item =
						listView_available.Items.Add(Helper.GetTypeName((DisplayFilterSettings.SegmentContentType)type));

					item.Group = GroupContentTypesAvailable;
					item.Tag = type;
				}

				foreach (var type in Enum.GetValues(typeof(DisplayFilterSettings.ConfirmationLevel)))
				{
					var item =
						listView_available.Items.Add(Helper.GetTypeName((DisplayFilterSettings.ConfirmationLevel)type));

					item.Group = GroupStatusAvailable;
					item.Tag = type;
				}

				foreach (var type in Enum.GetValues(typeof(OriginType)))
				{
					if (type.ToString() == "None")
						continue;

					var item =
						listView_available.Items.Add(Helper.GetTypeName((OriginType)type));

					item.Group = GroupOriginAvailable;

					item.Tag = type;
				}

				//edited fuzzy
				var editedFuzzy = listView_available.Items.Add("Edited Fuzzy");
				editedFuzzy.Group = GroupOriginAvailable;
				editedFuzzy.Tag = "EditedF";
				//unedited fuzzy
				var uneditedFuzzy = listView_available.Items.Add("Unedited Fuzzy");
				uneditedFuzzy.Group = GroupOriginAvailable;
				uneditedFuzzy.Tag = "UneditedF";

				foreach (var type in Enum.GetValues(typeof(OriginType)))
				{
					if (type.ToString() == "None")
					{
						continue;
					}

					var item = listView_available.Items.Add(Helper.GetTypeName((OriginType)type));
					item.Group = GroupPreviousOriginAvailable;
					item.Tag = type;
				}

				listView_available.Items[0].Selected = true;
			}
			finally
			{
				listView_available.EndUpdate();
				listView_selected.EndUpdate();
			}

			#endregion

			#region  |  comments panel  |

			textBox_commentText.Text = string.Empty;
			textBox_commentAuthor.Text = string.Empty;

			comboBox_commentSeverity.Items.Clear();
			foreach (var severity in Enum.GetValues(typeof(DisplayFilterSettings.CommentSeverityType)))
				comboBox_commentSeverity.Items.Add(severity.ToString());

			comboBox_commentSeverity.SelectedIndex = 0;
			#endregion

			#region  |  context info panel  |

			PopulateContextInfoList();

			#endregion

			#region |  segments panel  |

			evenBtn.Checked = false;
			oddBtn.Checked = false;
			groupedBtn.Checked = false;
			segmentsBox.Text = string.Empty;
			segmentsBox.Enabled = false;
			fuzzyMin.Text = string.Empty;
			fuzzyMax.Text = string.Empty;
			splitCheckBox.Checked = false;
			mergedCheckbox.Checked = false;
			mergedAcross.Checked = false;
			commentRegexBox.Checked = false;
			sourceSameBox.Checked = false;
			equalsCaseSensitive.Checked = false;
			_uniqueSegments = false;
			colorsListView.SelectedItems.Clear();
			_reverseFilter = false;
			containsTagsCheckBox.Checked = false;
			modifiedByBox.Text = string.Empty;
			modifiedByCheck.Checked = false;
			createdByBox.Text = string.Empty;
			createdByCheck.Checked = false;
			_unEditedFuzzy = false;
			_editedFuzzy = false;
			dsiLocation_textbox.Text = string.Empty;

			#endregion

			#region  |  filter status counter  |

			// initialize the filter status counter
			UpdateFilteredCountDisplay(0, 0);

			#endregion

			InitializeTabPageIcons();
			AvailableColorsList = new List<string>();
		}

		private static EditorController GetEditorController()
		{
			return SdlTradosStudio.Application.GetController<EditorController>();
		}

		private void AddColor(string color)
		{
			if (!string.IsNullOrEmpty(color) && !AvailableColorsList.Contains(color))
			{
				AvailableColorsList.Add(color);
			}
		}

		private void PopulateColorList()
		{
			try
			{
				AvailableColorsList.Clear();

				foreach (var segmentPair in _activeDocument.SegmentPairs)
				{
					var paragraphUnit = ColorPickerHelper.GetParagraphUnit(segmentPair);
					var colors = paragraphUnit != null
						? ColorPickerHelper.GetColorsList(paragraphUnit.Source, segmentPair.Source)
						: ColorPickerHelper.GetColorsList(segmentPair.Source);

					foreach (var color in colors)
					{
						AddColor(color);
					}

					if (colors.Count > 0)
					{
						continue;
					}

					var contextInfoList = segmentPair.GetParagraphUnitProperties().Contexts.Contexts;
					var colorCode = ColorPickerHelper.DefaultFormatingColorCode(contextInfoList);

					AddColor(colorCode);
				}

				SetAddColorsToListView();
			}
			catch
			{
				// catch all; ignore
			}

		}

		private void SetAddColorsToListView()
		{
			colorsListView.Items.Clear();
			if (AvailableColorsList != null)
			{
				foreach (var color in AvailableColorsList)
				{
					var hexaCode = string.Concat("#", color);
					var colorItem = new ListViewItem(hexaCode)
					{
						BackColor = ColorTranslator.FromHtml(hexaCode),
						ForeColor = Color.White
					};
					colorsListView.Items.Add(colorItem);
				}
			}
		}

		private void ApplyFilter(bool reverseSearch)
		{
			if (OnApplyDisplayFilter != null)
			{
				var result = new FilteredCountsCallback(UpdateFilteredCountDisplay);
				OnApplyDisplayFilter?.Invoke(DisplayFilterSettings, CustomFilterFilterSettings, reverseSearch, result);
			}
		}

		private void ClearFilter()
		{
			InitializeSettings();
			ApplyFilter(false);
		}

		private void SaveFilter()
		{
			var saveSettingsDialog = new SaveFileDialog
			{
				Title = StringResources.DisplayFilterControl_SaveFilter_Save_Filter_Settings,
				Filter = StringResources.DisplayFilterControl_Settings_XML_File_sdladfsettings
			};
			if (saveSettingsDialog.ShowDialog() != DialogResult.OK) return;

			var setting = new SavedSettings
			{
				CustomFilterSettings = CustomFilterFilterSettings,
				DisplayFilterSettings = DisplayFilterSettings
			};

			var settingsXml = DisplayFilterSerializer.SerializeSettings(setting);
			using (var sw = new StreamWriter(saveSettingsDialog.FileName, false, Encoding.UTF8))
			{
				sw.Write(settingsXml);
				sw.Flush();
			}
		}

		private void LoadFilter()
		{
			var loadSettingsDialog = new OpenFileDialog
			{
				Title = StringResources.DisplayFilterControl_LoadFilter_Load_Filter_Settings,
				Filter = StringResources.DisplayFilterControl_Settings_XML_File_sdladfsettings
			};

			if (loadSettingsDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			try
			{
				// read in the xml content
				string settingsXml;
				using (var sr = new StreamReader(loadSettingsDialog.FileName, Encoding.UTF8))
				{
					settingsXml = sr.ReadToEnd();
				}

				var savedSettings = DisplayFilterSerializer.DeserializeSettings<SavedSettings>(settingsXml);
				// deserialize the to the settings xml
				DisplayFilterSettings = savedSettings.DisplayFilterSettings;
				CustomFilterFilterSettings = savedSettings.CustomFilterSettings;

				ApplyFilter(false);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		private void ActiveDocument_DocumentFilterChanged(object sender, DocumentFilterEventArgs e)
		{
			if (e.DisplayFilter == null
				|| e.DisplayFilter.GetType() != typeof(DisplayFilter))
				InitializeSettings();

			UpdateFilteredCountDisplay(e.FilteredSegmentPairsCount, e.TotalSegmentPairsCount);
		}

		private void EditorController_ActiveDocumentChanged(object sender, DocumentEventArgs e)
		{
			ActiveDocumentChanged(e.Document);
		}

		private void ActiveDocumentChanged(Document document)
		{
			InitializeSettings();

			if (_activeDocument != null)
			{
				_activeDocument.DocumentFilterChanged -= ActiveDocument_DocumentFilterChanged;
			}

			// get a reference to the active document            
			_activeDocument = document;

			if (_activeDocument != null)
			{
				_activeDocument.DocumentFilterChanged += ActiveDocument_DocumentFilterChanged;

				SetContextInfoList();
				PopulateContextInfoList();

				if (_activeDocument.DisplayFilter != null &&
					_activeDocument.DisplayFilter.GetType() == typeof(DisplayFilter))
				{
					//invalidate UI with display settings recovered from the active document
					DisplayFilterSettings = ((DisplayFilter)_activeDocument.DisplayFilter).Settings;
				}

				PopulateColorList();

				UpdateFilteredCountDisplay(_activeDocument.FilteredSegmentPairsCount, _activeDocument.TotalSegmentPairsCount);
			}
		}

		private void ApplyDisplayFilter(DisplayFilterSettings displayFilterSettings, CustomFilterSettings customFilterSettings, bool reverse, FilteredCountsCallback result)
		{
			if (_activeDocument == null)
			{
				return;
			}

			DisplayFilter = new DisplayFilter(displayFilterSettings, customFilterSettings, reverse, _activeDocument);
			_activeDocument.ApplyFilterOnSegments(DisplayFilter);

			result.Invoke(_activeDocument.FilteredSegmentPairsCount, _activeDocument.TotalSegmentPairsCount);
		}

		private void UpdateFilteredCountDisplay(int filteredSegments, int totalSegments)
		{
			label_filterStatusBarMessage.Text =
				string.Format(StringResources.DisplayFilterControl_Filtered_of_rows
					, filteredSegments, totalSegments);

			UpdateFilterExpression();
			CheckEnabledFilterIcons();
		}

		private void UpdateFilterExpression()
		{
			filterExpressionControl.ClearItems();

			if (!string.IsNullOrEmpty(DisplayFilterSettings.SourceText)
				|| string.IsNullOrEmpty(DisplayFilterSettings.TargetText))
			{
				if (!string.IsNullOrEmpty(DisplayFilterSettings.SourceText))
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Source + ":\"" + DisplayFilterSettings.SourceText + "\"");
				}

				if (!string.IsNullOrEmpty(DisplayFilterSettings.SourceText) && !string.IsNullOrEmpty(DisplayFilterSettings.TargetText))
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Operator + ":\"" + CustomFilterFilterSettings.SourceAndTargetLogicalOperator + "\"");
				}

				if (!string.IsNullOrEmpty(DisplayFilterSettings.TargetText))
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Target + ":\"" + DisplayFilterSettings.TargetText + "\"");
				}

				if (DisplayFilterSettings.IsRegularExpression)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Regular_Expression + ":\"" + DisplayFilterSettings.IsRegularExpression + "\"");
				}

				if (CustomFilterFilterSettings.UseBackreferences)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Use_Backreferences + ":\"" + CustomFilterFilterSettings.UseBackreferences + "\"");
				}

				if (DisplayFilterSettings.IsCaseSensitive)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Case_Sensitive + ":\"" + DisplayFilterSettings.IsCaseSensitive + "\"");
				}
			}

			if (DisplayFilterSettings.ShowAllContent)
			{
				filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Show_All_Content
					+ ":\"" + DisplayFilterSettings.ShowAllContent + "\"");
			}

			if (DisplayFilterSettings.ConfirmationLevels.Any())
			{
				filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Status + ":"
					+ "(" + DisplayFilterSettings.ConfirmationLevels.Aggregate(string.Empty, (current, item) => current
					+ (current != string.Empty ? " " + "|" + " " : string.Empty)
					+ Helper.GetTypeName((DisplayFilterSettings.ConfirmationLevel)Enum.Parse(
						typeof(DisplayFilterSettings.ConfirmationLevel), item, true))) + ")");
			}

			try
			{
				if (!CustomFilterFilterSettings.EditedFuzzy && !CustomFilterFilterSettings.UnEditedFuzzy)
				{
					if (DisplayFilterSettings.OriginTypes.Any())
						filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Origin + ":"
														+ "(" + DisplayFilterSettings.OriginTypes.Aggregate(string.Empty,
															(current, item) => current
																			   + Helper.GetTypeName((OriginType)Enum.Parse(
																				   typeof(OriginType), item, true))) + ")");
				}
			}
			catch
			{
				// ignore; catch all
			}

			if (DisplayFilterSettings.PreviousOriginTypes.Any())
			{
				filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Previous_Origin + ":"
					+ "(" + DisplayFilterSettings.PreviousOriginTypes.Aggregate(string.Empty, (current, item) => current
					+ (current != string.Empty ? " " + "|" + " " : string.Empty)
					+ Helper.GetTypeName((OriginType)Enum.Parse(
						typeof(OriginType), item, true))) + ")");
			}

			if (_reverseFilter)
			{
				filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Reverse + ":\"" +
												_reverseFilter + "\"");
			}

			if (DisplayFilterSettings.SegmentReviewTypes.Any())
			{
				filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Segment_Review + ":"
					+ "(" + DisplayFilterSettings.SegmentReviewTypes.Aggregate(string.Empty, (current, item) => current
					+ (current != string.Empty ? " " + "|" + " " : string.Empty)
					+ Helper.GetTypeName((DisplayFilterSettings.SegmentReviewType)Enum.Parse(
						typeof(DisplayFilterSettings.SegmentReviewType), item, true))) + ")");
			}

			if (DisplayFilterSettings.SegmentLockingTypes.Any())
			{
				filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Segment_Locking + ":"
					+ "(" + DisplayFilterSettings.SegmentLockingTypes.Aggregate(string.Empty, (current, item) => current
					+ (current != string.Empty ? " " + "|" + " " : string.Empty)
					+ Helper.GetTypeName((DisplayFilterSettings.SegmentLockingType)Enum.Parse(
						typeof(DisplayFilterSettings.SegmentLockingType), item, true))) + ")");
			}

			if (DisplayFilterSettings.SegmentContentTypes.Any())
			{
				filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Segment_Content + ":"
					+ "(" + DisplayFilterSettings.SegmentContentTypes.Aggregate(string.Empty, (current, item) => current
					+ (current != string.Empty ? " " + "|" + " " : string.Empty)
					+ Helper.GetTypeName((DisplayFilterSettings.SegmentContentType)Enum.Parse(
						typeof(DisplayFilterSettings.SegmentContentType), item, true))) + ")");
			}

			if (DisplayFilterSettings.CommentText != string.Empty)
			{
				filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Comment_text + ":\"" + DisplayFilterSettings.CommentText + "\"");
			}

			if (DisplayFilterSettings.CommentAuthor != string.Empty)
			{
				filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Comment_author + ":\"" + DisplayFilterSettings.CommentAuthor + "\"");
			}

			if (DisplayFilterSettings.CommentSeverity > 0)
			{
				filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Comment_severity + ":\"" + (DisplayFilterSettings.CommentSeverityType)DisplayFilterSettings.CommentSeverity + "\"");
			}

			if (DisplayFilterSettings.ContextInfoTypes.Any())
			{
				filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Document_structure + ":"
					+ "(" + DisplayFilterSettings.ContextInfoTypes.Aggregate(string.Empty, (current, item) => current
					+ (current != string.Empty ? " " + "|" + " " : string.Empty)
					+ ContextInfoList.FirstOrDefault(a => a.ContextType == item)?.DisplayCode) + ")");
			}

			if (CustomFilterFilterSettings != null)
			{
				//filter color
				if (CustomFilterFilterSettings.Colors?.Count > 0)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Colors + ":"
								+ "(" + CustomFilterFilterSettings.Colors.Aggregate(string.Empty,
									(current, item) => current
														+ (current != string.Empty
															? " " + "|" + " "
															: string.Empty)
														+ CustomFilterFilterSettings.Colors.FirstOrDefault(a => a == item)) +
								")");
				}

				if (CustomFilterFilterSettings.SplitSegments)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_SplitSegments + ":\"" +
													CustomFilterFilterSettings.SplitSegments + "\"");
				}

				if (CustomFilterFilterSettings.MergedSegments)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_MergedSegments + ":\"" + CustomFilterFilterSettings.MergedSegments + "\"");
				}

				if (CustomFilterFilterSettings.MergedAcross)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_MergedAcross + ":\"" + CustomFilterFilterSettings.MergedAcross + "\"");
				}

				if (CustomFilterFilterSettings.EvenNo)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_EvenSegments + ":\"" +
													CustomFilterFilterSettings.EvenNo + "\"");
				}

				if (CustomFilterFilterSettings.OddsNo)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_OddSegments + ":\"" +
													CustomFilterFilterSettings.OddsNo + "\"");
				}

				if (CustomFilterFilterSettings.Grouped)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_GroupedList + ":\"" +
													CustomFilterFilterSettings.Grouped + "\"");
				}

				if (CustomFilterFilterSettings.UseRegexCommentSearch)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_UseRegexComments + ":\"" +
													CustomFilterFilterSettings.UseRegexCommentSearch + "\"");
				}

				if (CustomFilterFilterSettings.SourceEqualsTarget)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_SourceEqualsTarget + ":\"" +
													CustomFilterFilterSettings.SourceEqualsTarget + "\"");
				}

				if (CustomFilterFilterSettings.IsEqualsCaseSensitive)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_SourceEqualsTargetCDisplayFilterControl_SourceEqualsTargetCase + ":\"" +
													CustomFilterFilterSettings.IsEqualsCaseSensitive + "\"");
				}

				if (CustomFilterFilterSettings.FuzzyMax != string.Empty && CustomFilterFilterSettings.FuzzyMin != string.Empty)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Fuzzy + ":\"" + CustomFilterFilterSettings.FuzzyMin +
													" and " + CustomFilterFilterSettings.FuzzyMax + "\"");
				}

				if (CustomFilterFilterSettings.ContainsTags)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_Segments_With_tags + ":\"" +
													CustomFilterFilterSettings.ContainsTags + "\"");

				}
				if (CustomFilterFilterSettings.CreatedByChecked)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_CreatedBy + ":\"" +
													CustomFilterFilterSettings.CreatedBy + "\"");
				}
				if (CustomFilterFilterSettings.ModifiedByChecked)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_MidifiedBy + ":\"" +
													CustomFilterFilterSettings.ModifiedBy + "\"");
				}
				if (CustomFilterFilterSettings.EditedFuzzy)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_EditedFuzzy + ":\"" +
													CustomFilterFilterSettings.EditedFuzzy + "\"");
				}
				if (CustomFilterFilterSettings.UnEditedFuzzy)
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_UnEdited + ":\"" +
													CustomFilterFilterSettings.UnEditedFuzzy + "\"");
				}
				if (!string.IsNullOrEmpty(CustomFilterFilterSettings.ContextInfoStringId))
				{
					filterExpressionControl.AddItem(StringResources.DisplayFilterControl_StringId + ":\"" + CustomFilterFilterSettings.ContextInfoStringId + "\"");
				}

				if (CustomFilterFilterSettings.UseTagContent)
				{
					filterExpressionControl.AddItem(CustomFilterFilterSettings.AndOrTagContent ? StringResources.DisplayFilterControl_UseTagsAlso : StringResources.DisplayFilterControl_UseTagsOnly);
				}
			}
		}

	
		#region  |  Tab icons  |

		private void InitializeTabPageIcons()
		{
			tabPage_content.ImageIndex = -1;
			tabPage_filters.ImageIndex = -1;
			tabPage_comments.ImageIndex = -1;
			tabPage_contextInfo.ImageIndex = -1;
		}

		private void CheckEnabledFilterIcons()
		{
			if (_activeDocument != null)
			{
				if (_activeDocument.DisplayFilter != null
					&& _activeDocument.DisplayFilter.GetType() == typeof(DisplayFilter))
				{
					var settings = ((DisplayFilter)_activeDocument.DisplayFilter).Settings;

					InvalidateIconsFilterApplied_contentTab(settings);
					InvalidateIconsFilterApplied_filtersTab(settings);
					InvalidateIconsFilterApplied_commentsTab(settings);
					InvalidateIconsFilterApplied_contextInfoTab(settings);
					InvalidateIconsFilterApplied_segmentNumbers(CustomFilterFilterSettings);
					InvalidateIconsFilterApplied_colorPicker(CustomFilterFilterSettings);

					SetStatusBackgroundColorCode(IsFilterApplied(settings));
				}
				else
				{
					SetStatusBackgroundColorCode(false);

					InvalidateIconsFilterApplied(tabPage_content);
					InvalidateIconsFilterApplied(tabPage_filters);
					InvalidateIconsFilterApplied(tabPage_comments);
					InvalidateIconsFilterApplied(tabPage_contextInfo);
					InvalidateIconsFilterApplied(tabPage_segmentNumbers);
					InvalidateIconsFilterApplied(tabPage_Colors);

				}
			}
		}

		private void InvalidateIconsFilterApplied_colorPicker(CustomFilterSettings customFilter)
		{
			if (customFilter.Colors.Count > 0)
			{
				tabPage_Colors.ImageIndex = 0;
			}
			else
			{
				tabPage_Colors.ImageIndex = -1;
			}
		}

		private void SetStatusBackgroundColorCode(bool visible)
		{
			panel_filterStatusBarImage.Visible = visible;
			panel_filterStatusBar.BackColor = visible ? SystemColors.GradientInactiveCaption : Color.Transparent;
		}

		private static bool IsFilterApplied(DisplayFilterSettings settings)
		{
			if (!string.IsNullOrEmpty(settings.SourceText)
				|| !string.IsNullOrEmpty(settings.TargetText)
				|| settings.ContextInfoTypes.Any()
				|| settings.SegmentReviewTypes.Any()
				|| settings.ConfirmationLevels.Any()
				|| settings.OriginTypes.Any()
				|| settings.PreviousOriginTypes.Any()
				|| settings.RepetitionTypes.Any()
				|| settings.SegmentContentTypes.Any()
				|| settings.SegmentLockingTypes.Any()
				|| settings.SegmentReviewTypes.Any()
				|| settings.ShowAllContent
				|| !string.IsNullOrEmpty(settings.CommentText)
				|| !string.IsNullOrEmpty(settings.CommentAuthor)
				|| settings.CommentSeverity > 0)
			{
				return true;
			}
			return false;
		}

		private static void InvalidateIconsFilterApplied(TabPage tabPage)
		{
			tabPage.ImageIndex = -1;
		}

		private void InvalidateIconsFilterApplied_contentTab(DisplayFilterSettings settings)
		{
			if (!string.IsNullOrEmpty(settings.SourceText) ||
				!string.IsNullOrEmpty(settings.TargetText) ||
				!string.IsNullOrEmpty(CustomFilterFilterSettings.ContextInfoStringId))
			{
				tabPage_content.ImageIndex = 0;
			}
			else
			{
				tabPage_content.ImageIndex = -1;
			}
		}

		private void InvalidateIconsFilterApplied_filtersTab(DisplayFilterSettings settings)
		{
			if (settings.SegmentReviewTypes.Any()
				|| settings.ConfirmationLevels.Any()
				|| settings.OriginTypes.Any()
				|| settings.PreviousOriginTypes.Any()
				|| settings.RepetitionTypes.Any()
				|| settings.SegmentContentTypes.Any()
				|| settings.SegmentLockingTypes.Any()
				|| settings.SegmentReviewTypes.Any()
				|| settings.ShowAllContent)
			{
				tabPage_filters.ImageIndex = 0;
			}
			else
			{
				tabPage_filters.ImageIndex = -1;
			}
		}

		private void InvalidateIconsFilterApplied_commentsTab(DisplayFilterSettings settings)
		{
			if (!string.IsNullOrEmpty(settings.CommentText)
				|| !string.IsNullOrEmpty(settings.CommentAuthor)
				|| settings.CommentSeverity > 0)
			{
				tabPage_comments.ImageIndex = 0;
			}
			else
			{
				tabPage_comments.ImageIndex = -1;
			}
		}

		private void InvalidateIconsFilterApplied_contextInfoTab(DisplayFilterSettings settings)
		{
			if (settings.ContextInfoTypes.Any())
			{
				tabPage_contextInfo.ImageIndex = 0;
			}
			else
			{
				tabPage_contextInfo.ImageIndex = -1;
			}

		}

		private void InvalidateIconsFilterApplied_segmentNumbers(CustomFilterSettings customFilterSettings)
		{
			if (customFilterSettings.EvenNo || customFilterSettings.Grouped ||
				customFilterSettings.OddsNo || customFilterSettings.SplitSegments ||
				customFilterSettings.MergedSegments || customFilterSettings.SourceEqualsTarget ||
				!string.IsNullOrWhiteSpace(customFilterSettings.FuzzyMin) &&
				!string.IsNullOrWhiteSpace(customFilterSettings.FuzzyMax)
				|| customFilterSettings.MergedAcross || customFilterSettings.ContainsTags || customFilterSettings.CreatedByChecked
				|| customFilterSettings.ModifiedByChecked)
			{
				tabPage_segmentNumbers.ImageIndex = 0;
			}
			else
			{
				tabPage_segmentNumbers.ImageIndex = -1;
			}
		}

		private void InvalidateIconsFilterEdited(TabPage tabPage)
		{
			if (_activeDocument?.DisplayFilter != null && _activeDocument.DisplayFilter.GetType() == typeof(DisplayFilter))
			{
				var settings = ((DisplayFilter)_activeDocument.DisplayFilter).Settings;

				if (tabPage == tabPage_content)
				{
					if (!string.IsNullOrEmpty(settings.SourceText)
						|| !string.IsNullOrEmpty(settings.TargetText))
					{
						var andOrTagContent = alsoTags_radioButton.Checked
							? alsoTags_radioButton.Checked
							: onlyTags_radioButton.Checked;

						var item1 = textBox_source.Text + ", " +
									target_textbox.Text + ", " +
									checkBox_regularExpression.Checked + ", " +
									checkBox_caseSensitive.Checked + ',' +
									checkBox_TagContent.Checked + ',' +
									andOrTagContent + ',' +
									comboBox_SourceTargetFilterLogicalOperator.Text + ',' +
									checkBox_useBackReferences.Checked;

						var item2 = settings.SourceText + ", " +
									settings.TargetText + ", " +
									settings.IsRegularExpression + ", " +
									settings.IsCaseSensitive + ',' +
									_customFilterSettings.UseTagContent + ',' +
									_customFilterSettings.AndOrTagContent + ',' +
									_customFilterSettings.SourceAndTargetLogicalOperator + ',' +
									CustomFilterFilterSettings.UseBackreferences;

						tabPage.ImageIndex = string.CompareOrdinal(item1, item2) == 0 ? 0 : 1;
					}
				}
				else if (tabPage == tabPage_filters
					&& (settings.SegmentReviewTypes.Any()
					|| settings.ConfirmationLevels.Any()
					|| settings.OriginTypes.Any()
					|| settings.PreviousOriginTypes.Any()
					|| settings.RepetitionTypes.Any()
					|| settings.SegmentContentTypes.Any()
					|| settings.SegmentLockingTypes.Any()
					|| settings.SegmentReviewTypes.Any()
					|| settings.ShowAllContent))
				{
					var list1 = new List<string> { DisplayFilterSettings.ShowAllContent.ToString() };
					list1.AddRange(DisplayFilterSettings.OriginTypes);
					list1.AddRange(DisplayFilterSettings.PreviousOriginTypes);
					list1.AddRange(DisplayFilterSettings.ConfirmationLevels);
					list1.AddRange(DisplayFilterSettings.RepetitionTypes);
					list1.AddRange(DisplayFilterSettings.SegmentReviewTypes);
					list1.AddRange(DisplayFilterSettings.SegmentLockingTypes);
					list1.AddRange(DisplayFilterSettings.SegmentContentTypes);

					var list2 = new List<string> { settings.ShowAllContent.ToString() };
					list2.AddRange(settings.OriginTypes);
					list2.AddRange(settings.PreviousOriginTypes);
					list2.AddRange(settings.ConfirmationLevels);
					list2.AddRange(settings.RepetitionTypes);
					list2.AddRange(settings.SegmentReviewTypes);
					list2.AddRange(settings.SegmentLockingTypes);
					list2.AddRange(settings.SegmentContentTypes);

					tabPage.ImageIndex = string.Join(", ", list1) == string.Join(", ", list2)
						? 0
						: 1;
				}
				else if (tabPage == tabPage_comments
					&& (!string.IsNullOrEmpty(settings.CommentText)
					|| !string.IsNullOrEmpty(settings.CommentAuthor)
					|| settings.CommentSeverity > 0))
				{
					var item1 = textBox_commentText.Text + ", " + textBox_commentAuthor.Text + ", " +
								comboBox_commentSeverity.SelectedIndex;

					var item2 = settings.CommentText + ", " + settings.CommentAuthor + ", " +
								settings.CommentSeverity;

					tabPage.ImageIndex = string.CompareOrdinal(item1, item2) == 0 ? 0 : 1;
				}
				else if (tabPage == tabPage_contextInfo && settings.ContextInfoTypes.Any())
				{
					// check if identical selection
					var list = new List<string>();
					foreach (var contextInfo in listView_contextInfo.SelectedItems
						.Cast<ListViewItem>().Select(selectedItem => selectedItem.Tag as IContextInfo)
						.Where(contextInfo => contextInfo != null
											  && !list.Contains(contextInfo.ContextType)))
					{
						list.Add(contextInfo.ContextType);
					}
					tabPage.ImageIndex = string.Join(", ", list) == string.Join(", ", settings.ContextInfoTypes)
						? 0
						: 1;
				}
				else
					tabPage.ImageIndex = 1;
			}
			else
			{
				tabPage.ImageIndex = 1;
			}
		}

		#endregion
		
		#region  |  ToolbarStrip  |

		private void ToolStripButton_applyFilter_Click(object sender, EventArgs e)
		{
			ApplyFilter(false);
		}

		private void ToolStripButton_clearFilter_Click(object sender, EventArgs e)
		{
			ClearFilter();
		}

		private void ToolStripButton_saveFilter_Click(object sender, EventArgs e)
		{
			SaveFilter();
		}

		private void ToolStripButton_loadFilter_Click(object sender, EventArgs e)
		{
			LoadFilter();
		}

		private void ReverseBtn_Click(object sender, EventArgs e)
		{
			_reverseFilter = true;
			ApplyFilter(true);
		}

		private void HelpButton_Click(object sender, EventArgs e)
		{
			System.Diagnostics.Process.Start("https://community.sdl.com/product-groups/translationproductivity/w/customer-experience/3130.community-advanced-display-filter");
		}

		private void GenerateXliff_Click(object sender, EventArgs e)
		{
			var segments = _activeDocument?.FilteredSegmentPairs?.ToList();

			//list with ids of segments from filter result 
			if (segments == null)
			{
				return;
			}

			var segmentsIds = segments.Select(segment => segment.Properties.Id.Id).ToList();
			var saveFileDialog = new SaveFileDialog
			{
				Filter = @"sdlxliff files (*.sdlxliff)|*.sdlxliff|All files (*.*)|*."
			};

			if (saveFileDialog.ShowDialog() == DialogResult.OK)
			{
				var selectedFilePath = saveFileDialog.FileName;

				if (File.Exists(selectedFilePath))
				{
					File.Delete(selectedFilePath);
				}

				var activeFilePath = _activeDocument?.ActiveFile?.LocalFilePath;

				if (activeFilePath != null)
				{
					File.Copy(activeFilePath, selectedFilePath);
					var xliffParser = new XliffParser(selectedFilePath, segmentsIds);
					xliffParser.GenerateXliff();
				}

				MessageBox.Show(@"File was generated at the following location: " + selectedFilePath, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		}

		private void SetupHighlightMenu()
		{
			if (highlightColorsToolStripMenuItem.DropDownItems.Count > 0)
			{
				foreach (var toolStripItem in highlightColorsToolStripMenuItem.DropDownItems.Cast<ToolStripItem>())
				{
					toolStripItem.Click -= HighlightToolStripItem_Click;
				}
			}

			highlightColorsToolStripMenuItem.DropDownItems.Clear();

			foreach (var highlightColor in _highlightService.GetHighlightColors())
			{
				var toolStripItem = new ToolStripMenuItem
				{
					Text = highlightColor.DisplayName,
					Image = highlightColor.Image,
					Tag = highlightColor
				};
				toolStripItem.Click += HighlightToolStripItem_Click;

				highlightColorsToolStripMenuItem.DropDownItems.Add(toolStripItem);
			}
		}

		private void HighlightToolStripItem_Click(object sender, EventArgs e)
		{
			if (sender is ToolStripMenuItem toolStripButton && toolStripButton.Tag is HighlightColor highlightColor)
			{
				_highlightService.ApplyHighlightColor(_activeDocument, HighlightService.HighlightScope.Filtered, highlightColor);
			}
		}

		private void ClearHighlightingToolStripMenuItem_Click(object sender, EventArgs e)
		{
			_highlightService.ClearHighlightColors(_activeDocument, HighlightService.HighlightScope.Filtered);
		}

		#endregion

		#region  |  Content tab  |

		private void TextBox_source_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return)
				ApplyFilter(false);
		}

		private void TextBox_target_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return)
				ApplyFilter(false);
		}

		private void TextBox_source_TextChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_content);
		}

		private void TextBox_target_TextChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_content);
		}

		private void CheckBox_regularExpression_CheckedChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_content);

			var value = ((CheckBox)sender).Checked;
			if (value)
			{
				checkBox_useBackReferences.Enabled = true;
			}
			else
			{
				checkBox_useBackReferences.Checked = false;
				checkBox_useBackReferences.Enabled = false;
			}

		}

		private void CheckBox_useBackReferences_CheckedChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_content);

			var value = ((CheckBox)sender).Checked;
			if (value)
			{
				comboBox_SourceTargetFilterLogicalOperator.Enabled = false;
				comboBox_SourceTargetFilterLogicalOperator.SelectedIndex = 0;
			}
			else
			{
				comboBox_SourceTargetFilterLogicalOperator.Enabled = true;
			}
		}

		private void ComboBox_SourceTargetFilterLogicalOperator_SelectedIndexChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_content);
		}

		private void ComboBox_SourceTargetFilterLogicalOperator_KeyPress(object sender, KeyPressEventArgs e)
		{
			e.KeyChar = (char)Keys.None;
		}

		private void CheckBox_caseSensitive_CheckedChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_content);
		}

		private void OnlyTags_radioButton_CheckedChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_content);
		}

		private void DsiLocation_textbox_TextChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_content);
		}

		private void CheckBox_TagContent_CheckedChanged(object sender, EventArgs e)
		{
			if (sender is CheckBox chkBox)
			{
				content_groupBox.Enabled = chkBox.Checked;
				InvalidateIconsFilterEdited(tabPage_content);
			}
		}

		private void AlsoTags_radioButton_CheckedChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_content);
		}


		#endregion

		#region  |  Filter Attributes tab  |
	
		private void CheckEnabledActionButtons()
		{
			var add = true;
			var remove = true;
			var removeAll = true;

			if (listView_available.SelectedItems.Count == 0)
				add = false;

			if (listView_selected.SelectedItems.Count == 0)
				remove = false;

			if (listView_selected.Items.Count == 0)
				removeAll = false;

			button_add.Enabled = add;
			button_remove.Enabled = remove;
			button_removeAll.Enabled = removeAll;
		}

		private void MoveSelectedListViewItem(ListView from, ListView to)
		{
			if (from.SelectedItems.Count > 0)
			{
				var itemIndex = 0;
				foreach (ListViewItem itemFrom in from.SelectedItems)
				{
					itemIndex = itemFrom.Index;

					var itemTo = to.Items.Add((ListViewItem)itemFrom.Clone());

					AssignOriginTypeListViewItemGroup(itemTo, itemFrom.Group);


					itemFrom.Remove();
				}
				SelectDefaultItem(from, itemIndex);

				listView_available.Sort();
				listView_selected.Sort();

				CheckEnabledActionButtons();
			}
		}

		private static void SelectDefaultItem(ListView from, int itemIndex)
		{
			if (from.Items.Count > 0)
			{
				foreach (var item in from.Items.Cast<ListViewItem>())
					item.Selected = false;

				if (from.Items.Count - 1 < itemIndex)
					itemIndex--;

				from.Items[itemIndex].Selected = true;
			}
		}

		private void MoveListViewItem(ListView from, ListViewItem itemFrom, ListView to)
		{
			var itemIndex = itemFrom.Index;

			var itemTo = to.Items.Add((ListViewItem)itemFrom.Clone());

			AssignOriginTypeListViewItemGroup(itemTo, itemFrom.Group);

			itemFrom.Remove();

			SelectDefaultItem(from, itemIndex);

			listView_available.Sort();
			listView_selected.Sort();

			CheckEnabledActionButtons();

		}

		private void MoveAllListViewItems(ListView from, ListView to)
		{
			foreach (ListViewItem itemFrom in from.Items)
			{
				var itemTo = to.Items.Add((ListViewItem)itemFrom.Clone());
				AssignOriginTypeListViewItemGroup(itemTo, itemFrom.Group);

				itemFrom.Remove();
			}

			listView_available.Sort();
			listView_selected.Sort();

			CheckEnabledActionButtons();
		}

		private void AssignOriginTypeListViewItemGroup(ListViewItem itemTo, ListViewGroup fromGroup)
		{
			if (itemTo.ListView.Equals(listView_available)
				|| itemTo.ListView.Equals(listView_selected))
			{
				if (itemTo.ListView.Equals(listView_available))
				{
					if (fromGroup == GroupOriginSelected)
						itemTo.Group = GroupOriginAvailable;
					else if (fromGroup == GroupPreviousOriginSelected)
						itemTo.Group = GroupPreviousOriginAvailable;
					else if (fromGroup == GroupStatusSelected)
						itemTo.Group = GroupStatusAvailable;
					else if (fromGroup == GroupGeneralSelected)
						itemTo.Group = GroupGeneralAvailable;
					else if (fromGroup == GroupRepetitionTypesSelected)
						itemTo.Group = GroupRepetitionTypesAvailable;
					else if (fromGroup == GroupReviewTypesSelected)
						itemTo.Group = GroupReviewTypesAvailable;
					else if (fromGroup == GroupLockingTypesSelected)
						itemTo.Group = GroupLockingTypesAvailable;
					else if (fromGroup == GroupContentTypesSelected)
						itemTo.Group = GroupContentTypesAvailable;
				}
				else
				{
					if (fromGroup == GroupOriginAvailable)
						itemTo.Group = GroupOriginSelected;
					else if (fromGroup == GroupPreviousOriginAvailable)
						itemTo.Group = GroupPreviousOriginSelected;
					else if (fromGroup == GroupStatusAvailable)
						itemTo.Group = GroupStatusSelected;
					else if (fromGroup == GroupGeneralAvailable)
						itemTo.Group = GroupGeneralSelected;
					else if (fromGroup == GroupRepetitionTypesAvailable)
						itemTo.Group = GroupRepetitionTypesSelected;
					else if (fromGroup == GroupReviewTypesAvailable)
						itemTo.Group = GroupReviewTypesSelected;
					else if (fromGroup == GroupLockingTypesAvailable)
						itemTo.Group = GroupLockingTypesSelected;
					else if (fromGroup == GroupContentTypesAvailable)
						itemTo.Group = GroupContentTypesSelected;
				}
			}
		}

		private void AddGroupsToOriginTypeListview()
		{
			listView_available.ShowGroups = true;
			listView_selected.ShowGroups = true;

			GroupGeneralAvailable = new ListViewGroup(StringResources.DisplayFilterControl_General);
			GroupStatusAvailable = new ListViewGroup(StringResources.DisplayFilterControl_Status);
			GroupOriginAvailable = new ListViewGroup(StringResources.DisplayFilterControl_Origin);
			GroupPreviousOriginAvailable = new ListViewGroup(StringResources.DisplayFilterControl_Previous_Origin);
			GroupRepetitionTypesAvailable = new ListViewGroup(StringResources.DisplayFilterControl_Repetitions);
			GroupReviewTypesAvailable = new ListViewGroup(StringResources.DisplayFilterControl_Segment_Review);
			GroupLockingTypesAvailable = new ListViewGroup(StringResources.DisplayFilterControl_Segment_Locking);
			GroupContentTypesAvailable = new ListViewGroup(StringResources.DisplayFilterControl_Segment_Content);

			listView_available.Groups.Add(GroupGeneralAvailable);
			listView_available.Groups.Add(GroupStatusAvailable);
			listView_available.Groups.Add(GroupOriginAvailable);
			listView_available.Groups.Add(GroupPreviousOriginAvailable);
			listView_available.Groups.Add(GroupRepetitionTypesAvailable);
			listView_available.Groups.Add(GroupReviewTypesAvailable);
			listView_available.Groups.Add(GroupLockingTypesAvailable);
			listView_available.Groups.Add(GroupContentTypesAvailable);

			GroupGeneralSelected = new ListViewGroup(StringResources.DisplayFilterControl_General);
			GroupStatusSelected = new ListViewGroup(StringResources.DisplayFilterControl_Status);
			GroupOriginSelected = new ListViewGroup(StringResources.DisplayFilterControl_Origin);
			GroupPreviousOriginSelected = new ListViewGroup(StringResources.DisplayFilterControl_Previous_Origin);
			GroupRepetitionTypesSelected = new ListViewGroup(StringResources.DisplayFilterControl_Repetitions);
			GroupReviewTypesSelected = new ListViewGroup(StringResources.DisplayFilterControl_Segment_Review);
			GroupLockingTypesSelected = new ListViewGroup(StringResources.DisplayFilterControl_Segment_Locking);
			GroupContentTypesSelected = new ListViewGroup(StringResources.DisplayFilterControl_Segment_Content);

			listView_selected.Groups.Add(GroupGeneralSelected);
			listView_selected.Groups.Add(GroupStatusSelected);
			listView_selected.Groups.Add(GroupOriginSelected);
			listView_selected.Groups.Add(GroupPreviousOriginSelected);
			listView_selected.Groups.Add(GroupRepetitionTypesSelected);
			listView_selected.Groups.Add(GroupReviewTypesSelected);
			listView_selected.Groups.Add(GroupLockingTypesSelected);
			listView_selected.Groups.Add(GroupContentTypesSelected);
		}

		private void Button_add_Click(object sender, EventArgs e)
		{
			var isSelected = IsUniqueSelected();
			if (isSelected)
			{
				_uniqueSegments = true;
			}
			var isEditedSelected = IsEditedFuzzySelected();
			if (isEditedSelected)
			{
				_editedFuzzy = true;
			}
			var isUnEditedSelected = IsUnEditedFuzzySelected();
			if (isUnEditedSelected)
			{
				_unEditedFuzzy = true;
			}
			if(IsMTSelected())
			{
				_MT = true;
			}
			MoveSelectedListViewItem(listView_available, listView_selected);
			InvalidateIconsFilterEdited(tabPage_filters);
		}

		private bool IsEditedFuzzySelected()
		{
			foreach (ListViewItem selectedItem in listView_available.SelectedItems)
			{

				if (selectedItem.Tag.Equals("EditedF"))
				{
					return true;
				}
			}
			return false;
		}
		private bool IsMTSelected()
		{
			foreach (ListViewItem selectedItem in listView_available.SelectedItems)
			{

				if (selectedItem.Tag.Equals("MT"))
				{
					return true;
				}
			}
			return false;
		}

		private bool IsUnEditedFuzzySelected()
		{
			foreach (ListViewItem selectedItem in listView_available.SelectedItems)
			{

				if (selectedItem.Tag.Equals("UneditedF"))
				{
					return true;
				}
			}
			return false;
		}

		private bool IsUniqueSelected()
		{
			foreach (ListViewItem selectedItem in listView_available.SelectedItems)
			{

				if (selectedItem.Tag.Equals("Unique"))
				{
					return true;
				}
			}
			return false;
		}

		private void Button_remove_Click(object sender, EventArgs e)
		{
			if (IsUniqueSelected())
			{
				_uniqueSegments = false;
			}
			if (IsEditedFuzzySelected())
			{
				_editedFuzzy = false;
			}
			if (IsUnEditedFuzzySelected())
			{
				_unEditedFuzzy = false;
			}
			if (IsMTSelected())
			{
				_MT = false;
			}
			MoveSelectedListViewItem(listView_selected, listView_available);
			InvalidateIconsFilterEdited(tabPage_filters);
		}

		private void Button_removeAll_Click(object sender, EventArgs e)
		{
			MoveAllListViewItems(listView_selected, listView_available);
			InvalidateIconsFilterEdited(tabPage_filters);
		}

		private void ListView_available_SelectedIndexChanged(object sender, EventArgs e)
		{
			CheckEnabledActionButtons();
		}

		private void ListView_selected_SelectedIndexChanged(object sender, EventArgs e)
		{
			CheckEnabledActionButtons();
		}

		private void ListView_available_Resize(object sender, EventArgs e)
		{
			var width = ((ListView)sender).Width - 20 - SystemInformation.VerticalScrollBarWidth;
			columnHeader_filtersAvailable_name.Width = width;
		}

		private void ListView_selected_Resize(object sender, EventArgs e)
		{
			var width = ((ListView)sender).Width - 20 - SystemInformation.VerticalScrollBarWidth;
			columnHeader_filtersSelected_name.Width = width;
		}

		private void ListView_available_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			if (IsUniqueSelected())
			{
				_uniqueSegments = true;
			}

			if (IsEditedFuzzySelected())
			{
				_editedFuzzy = true;
			}

			if (IsUnEditedFuzzySelected())
			{
				_unEditedFuzzy = true;
			}

			MoveSelectedListViewItem(listView_available, listView_selected);
			InvalidateIconsFilterEdited(tabPage_filters);
		}

		#endregion

		#region  |  Comments tab  |

		private void TextBox_commentText_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return)
				ApplyFilter(false);
		}

		private void TextBox_commentAuthor_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return)
				ApplyFilter(false);
		}

		private void TextBox_commentText_TextChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_comments);
		}

		private void TextBox_commentAuthor_TextChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_comments);
		}

		private void ComboBox_commentSeverity_SelectedIndexChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_comments);
		}

		private void CommentRegexBox_CheckedChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_comments);
		}

		#endregion

		#region  |  Context info tab  |

		private void SetContextInfoList()
		{
			ContextInfoList = new List<IContextInfo>();
			foreach (var segmentPair in _activeDocument.SegmentPairs)
			{
				var contexts = segmentPair.GetParagraphUnitProperties().Contexts;
				if (contexts == null) continue;
				foreach (var contextInfo in contexts.Contexts
					.Where(a => a.DisplayCode != null)
					.Where(contextInfo => ContextInfoList.All(a => a.ContextType != contextInfo.ContextType)))
				{
					ContextInfoList.Add(contextInfo);
				}
			}
		}

		private void PopulateContextInfoList()
		{
			try
			{
				listView_contextInfo.BeginUpdate();
				listView_contextInfo.Items.Clear();

				if (ContextInfoList == null) return;
				foreach (var contextInfo in ContextInfoList)
				{
					var item = listView_contextInfo.Items.Add(contextInfo.DisplayCode);
					item.SubItems.Add(contextInfo.DisplayName);
					item.SubItems.Add(contextInfo.Description ?? contextInfo.ContextType);
					item.BackColor = contextInfo.DisplayColor;
					item.UseItemStyleForSubItems = false;
					item.Tag = contextInfo;
				}
			}
			finally
			{
				listView_contextInfo.EndUpdate();
			}

			UpdatedContextInfoSelectedStatusCount();
		}

		private void LinkLabel_contextInfoClearSelection_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			listView_contextInfo.BeginUpdate();
			foreach (ListViewItem item in listView_contextInfo.Items)
				item.Selected = false;
			listView_contextInfo.EndUpdate();

			if (_activeDocument != null && _activeDocument.DisplayFilter != null
				&& _activeDocument.DisplayFilter.GetType() == typeof(DisplayFilter))
			{
				var settings = ((DisplayFilter)_activeDocument.DisplayFilter).Settings;

				if (settings.ContextInfoTypes.Any())
				{
					tabPage_contextInfo.ImageIndex = 1;
				}
				else
				{
					tabPage_contextInfo.ImageIndex = -1;
				}
			}
			else
			{
				tabPage_contextInfo.ImageIndex = -1;
			}

			UpdatedContextInfoSelectedStatusCount();
		}

		private void ListView_contextInfo_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_contextInfo);
			UpdatedContextInfoSelectedStatusCount();
		}

		private void UpdatedContextInfoSelectedStatusCount()
		{
			label_contextInfoSelected.Text = string.Format("Selected: {0}", listView_contextInfo.SelectedItems.Count);
		}

		private void ListView_contextInfo_Resize(object sender, EventArgs e)
		{
			var width = ((ListView)sender).Width - 20 - SystemInformation.VerticalScrollBarWidth;

			columnHeader_code.Width = Convert.ToInt32(width * .2);
			columnHeader_name.Width = Convert.ToInt32(width * .4);
			columnHeader_description.Width = Convert.ToInt32(width * .45);
		}

		#endregion

		#region  |  Segment tab |

		private void EvenBtn_CheckedChanged(object sender, EventArgs e)
		{
			if (evenBtn.Checked)
			{
				segmentsBox.Enabled = false;
			}
			InvalidateIconsFilterEdited(tabPage_segmentNumbers);
		}

		private void OddBtn_CheckedChanged(object sender, EventArgs e)
		{
			if (oddBtn.Checked)
			{
				segmentsBox.Enabled = false;
			}
			InvalidateIconsFilterEdited(tabPage_segmentNumbers);
		}

		private void GroupedBtn_CheckedChanged(object sender, EventArgs e)
		{
			if (groupedBtn.Checked)
			{
				segmentsBox.Enabled = true;
			}
			InvalidateIconsFilterEdited(tabPage_segmentNumbers);
		}

		private void NoneBtn_CheckedChanged(object sender, EventArgs e)
		{
			if (noneBtn.Checked)
			{
				segmentsBox.Enabled = false;
			}
			InvalidateIconsFilterEdited(tabPage_segmentNumbers);
		}

		private void SplitCheckBox_CheckedChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_segmentNumbers);
		}

		private void MergedCheckbox_CheckedChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_segmentNumbers);
		}

		private void SourceSameBox_CheckedChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_segmentNumbers);
		}

		private void EqualsCaseSensitive_CheckedChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_segmentNumbers);
		}

		private void FuzzyMin_TextChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_segmentNumbers);
		}

		private void FuzzyMax_TextChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_segmentNumbers);
		}

		private void MergedAcross_CheckedChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_segmentNumbers);
		}

		private void ContainsTagsCheckBox_CheckedChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_segmentNumbers);
		}

		private void ModifiedByCheck_CheckedChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_segmentNumbers);
		}

		private void CreatedByCheck_CheckedChanged(object sender, EventArgs e)
		{
			InvalidateIconsFilterEdited(tabPage_segmentNumbers);
		}

		#endregion

		private void listView_available_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			if (IsUniqueSelected())
			{
				_uniqueSegments = true;
			}
			if (IsEditedFuzzySelected())
			{
				_editedFuzzy = true;
			}
			if (IsUnEditedFuzzySelected())
			{
				_unEditedFuzzy = true;
			}
			if (IsMTSelected())
			{
				_MT = true;
			}
			MoveSelectedListViewItem(listView_available, listView_selected);
			InvalidateIconsFilterEdited(tabPage_filters);
		}
    
		#region  |  Color tab |

		private void ColorsListView_SelectedIndexChanged(object sender, EventArgs e)
		{
			var selectedColors = colorsListView.SelectedItems;

			if (AvailableColorsList != null)
			{
				AvailableColorsList.Clear();
				foreach (ListViewItem color in selectedColors)
				{
					var colorCode = color.Text;

					if (!AvailableColorsList.Contains(colorCode))
					{
						AvailableColorsList.Add(colorCode);
					}
				}
			}
			InvalidateIconsFilterEdited(tabPage_Colors);
		}

		#endregion
	}
}