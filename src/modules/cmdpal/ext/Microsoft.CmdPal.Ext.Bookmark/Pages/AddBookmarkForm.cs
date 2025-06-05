﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Models;
using Microsoft.CmdPal.Ext.Bookmarks.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Bookmarks;

internal sealed partial class AddBookmarkForm : FormContent
{
    internal event TypedEventHandler<object, BookmarkData>? AddedCommand;

    private readonly BookmarkData? _bookmark;

    public AddBookmarkForm(BookmarkData? bookmark)
    {
        _bookmark = bookmark;
        var name = _bookmark?.Name ?? string.Empty;
        var url = _bookmark?.Bookmark ?? string.Empty;

        TemplateJson = $$"""
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.5",
    "body": [
        {
            "type": "Input.Text",
            "style": "text",
            "id": "name",
            "label": {{JsonSerializer.Serialize(Resources.bookmarks_form_name_label, BookmarkSerializationContext.Default.String)}},
            "value": {{JsonSerializer.Serialize(name, BookmarkSerializationContext.Default.String)}},
            "isRequired": true,
            "errorMessage": "{{JsonSerializer.Serialize(Resources.bookmarks_form_name_required, BookmarkSerializationContext.Default.String)}}"
        },
        {
            "type": "Input.Text",
            "style": "text",
            "id": "bookmark",
            "value": {{JsonSerializer.Serialize(url, BookmarkSerializationContext.Default.String)}},
            "label": {{JsonSerializer.Serialize(Resources.bookmarks_form_bookmark_label, BookmarkSerializationContext.Default.String)}},
            "isRequired": true,
            "errorMessage": "{{JsonSerializer.Serialize(Resources.bookmarks_form_bookmark_required, BookmarkSerializationContext.Default.String)}}"
        }
    ],
    "actions": [
        {
            "type": "Action.Submit",
            "title": {{JsonSerializer.Serialize(Resources.bookmarks_form_save, BookmarkSerializationContext.Default.String)}},
            "data": {
                "name": "name",
                "bookmark": "bookmark"
            }
        }
    ]
}
""";
    }

    public override CommandResult SubmitForm(string payload)
    {
        var formInput = JsonNode.Parse(payload);
        if (formInput == null)
        {
            return CommandResult.GoHome();
        }

        // get the name and url out of the values
        var formName = formInput["name"] ?? string.Empty;
        var formBookmark = formInput["bookmark"] ?? string.Empty;

        var updated = _bookmark ?? new BookmarkData();
        updated.Name = formName.ToString();
        updated.Bookmark = formBookmark.ToString();
        updated.Type = BookmarkTypeHelper.GetBookmarkTypeFromValue(formBookmark.ToString());

        AddedCommand?.Invoke(this, updated);
        return CommandResult.GoHome();
    }
}
