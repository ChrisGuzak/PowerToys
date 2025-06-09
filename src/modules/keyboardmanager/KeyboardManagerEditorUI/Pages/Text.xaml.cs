// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Interop;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace KeyboardManagerEditorUI.Pages
{
    public sealed partial class Text : Page, IDisposable
    {
        private KeyboardMappingService? _mappingService;

        // Flag to indicate if the user is editing an existing mapping
        private bool _isEditMode;
        private TextMapping? _editingMapping;

        private bool _disposed;

        // The list of text mappings
        public ObservableCollection<TextMapping> TextMappings { get; } = new ObservableCollection<TextMapping>();

        public Text()
        {
            this.InitializeComponent();

            try
            {
                _mappingService = new KeyboardMappingService();
                LoadTextMappings();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize KeyboardMappingService: " + ex.Message);
            }

            this.Unloaded += Text_Unloaded;
        }

        private void Text_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        private void LoadTextMappings()
        {
            if (_mappingService == null)
            {
                return;
            }

            TextMappings.Clear();

            // Load key-to-text mappings
            var keyToTextMappings = _mappingService.GetKeyToTextMappings();
            foreach (var mapping in keyToTextMappings)
            {
                TextMappings.Add(new TextMapping
                {
                    Keys = new List<string> { _mappingService.GetKeyDisplayName(mapping.OriginalKey) },
                    Text = mapping.TargetText,
                    IsAllApps = true,
                    AppName = "All Apps",
                });
            }

            // Load shortcut-to-text mappings
            foreach (var mapping in _mappingService.GetShortcutMappingsByType(ShortcutOperationType.RemapText))
            {
                string[] originalKeyCodes = mapping.OriginalKeys.Split(';');
                var originalKeyNames = new List<string>();
                foreach (var keyCode in originalKeyCodes)
                {
                    if (int.TryParse(keyCode, out int code))
                    {
                        originalKeyNames.Add(_mappingService.GetKeyDisplayName(code));
                    }
                }

                TextMappings.Add(new TextMapping
                {
                    Keys = originalKeyNames,
                    Text = mapping.TargetText,
                    IsAllApps = string.IsNullOrEmpty(mapping.TargetApp),
                    AppName = string.IsNullOrEmpty(mapping.TargetApp) ? "All Apps" : mapping.TargetApp,
                });
            }
        }

        private async void NewShortcutBtn_Click(object sender, RoutedEventArgs e)
        {
            _isEditMode = false;
            _editingMapping = null;

            TextInputControl.ClearKeys();
            TextInputControl.SetTextContent(string.Empty);
            TextInputControl.SetAppSpecific(false, string.Empty);

            await KeyDialog.ShowAsync();
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TextMapping selectedMapping)
            {
                _isEditMode = true;
                _editingMapping = selectedMapping;

                TextInputControl.SetShortcutKeys(selectedMapping.Keys);
                TextInputControl.SetTextContent(selectedMapping.Text);
                TextInputControl.SetAppSpecific(!selectedMapping.IsAllApps, selectedMapping.AppName);

                await KeyDialog.ShowAsync();
            }
        }

        private void KeyDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (_mappingService == null)
            {
                return;
            }

            List<string> keys = TextInputControl.GetShortcutKeys();
            string textContent = TextInputControl.GetTextContent();
            bool isAppSpecific = TextInputControl.GetIsAppSpecific();
            string appName = TextInputControl.GetAppName();

            // Validate inputs
            ValidationErrorType errorType = ValidationHelper.ValidateTextMapping(
                keys, textContent, isAppSpecific, appName, _mappingService);

            if (errorType != ValidationErrorType.NoError)
            {
                ShowValidationError(errorType, args);
                return;
            }

            bool saved = false;

            try
            {
                // Delete existing mapping if in edit mode
                if (_isEditMode && _editingMapping != null)
                {
                    if (_editingMapping.Keys.Count == 1)
                    {
                        int originalKey = _mappingService.GetKeyCodeFromName(_editingMapping.Keys[0]);
                        if (originalKey != 0)
                        {
                            _mappingService.DeleteSingleKeyToTextMapping(originalKey);
                        }
                    }
                    else
                    {
                        string originalKeys = string.Join(";", _editingMapping.Keys.Select(k => _mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
                        _mappingService.DeleteShortcutMapping(originalKeys, _editingMapping.IsAllApps ? string.Empty : _editingMapping.AppName);
                    }
                }

                // Add new mapping
                if (keys.Count == 1)
                {
                    // Single key to text mapping
                    int originalKey = _mappingService.GetKeyCodeFromName(keys[0]);
                    if (originalKey != 0)
                    {
                        saved = _mappingService.AddSingleKeyToTextMapping(originalKey, textContent);
                    }
                }
                else
                {
                    // Shortcut to text mapping
                    string originalKeysString = string.Join(";", keys.Select(k => _mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));

                    if (isAppSpecific && !string.IsNullOrEmpty(appName))
                    {
                        saved = _mappingService.AddShortcutMapping(originalKeysString, textContent, appName, ShortcutOperationType.RemapText);
                    }
                    else
                    {
                        saved = _mappingService.AddShortcutMapping(originalKeysString, textContent, operationType: ShortcutOperationType.RemapText);
                    }
                }

                if (saved)
                {
                    _mappingService.SaveSettings();
                    LoadTextMappings(); // Refresh the list
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error saving text mapping: " + ex.Message);
                args.Cancel = true;
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mappingService == null || !(sender is Button button) || !(button.DataContext is TextMapping mapping))
            {
                return;
            }

            try
            {
                bool deleted = false;
                if (mapping.Keys.Count == 1)
                {
                    // Single key mapping
                    int originalKey = _mappingService.GetKeyCodeFromName(mapping.Keys[0]);
                    if (originalKey != 0)
                    {
                        deleted = _mappingService.DeleteSingleKeyToTextMapping(originalKey);
                    }
                }
                else
                {
                    // Shortcut mapping
                    string originalKeys = string.Join(";", mapping.Keys.Select(k => _mappingService.GetKeyCodeFromName(k)));
                    deleted = _mappingService.DeleteShortcutMapping(originalKeys, mapping.IsAllApps ? string.Empty : mapping.AppName);
                }

                if (deleted)
                {
                    _mappingService.SaveSettings();
                    TextMappings.Remove(mapping);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error deleting text mapping: " + ex.Message);
            }
        }

        private void ShowValidationError(ValidationErrorType errorType, ContentDialogButtonClickEventArgs args)
        {
            if (ValidationHelper.ValidationMessages.TryGetValue(errorType, out (string Title, string Message) error))
            {
                ValidationTip.Title = error.Title;
                ValidationTip.Subtitle = error.Message;
                ValidationTip.IsOpen = true;
                args.Cancel = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _mappingService?.Dispose();
                    _mappingService = null;
                }

                _disposed = true;
            }
        }
    }
}
