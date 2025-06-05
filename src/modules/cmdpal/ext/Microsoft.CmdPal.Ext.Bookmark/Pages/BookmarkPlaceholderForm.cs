﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Bookmarks.Command;
using Microsoft.CmdPal.Ext.Bookmarks.Models;
using Microsoft.CmdPal.Ext.Bookmarks.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Bookmarks;

internal sealed partial class BookmarkPlaceholderForm : FormContent
{
    private static readonly CompositeFormat ErrorMessage = System.Text.CompositeFormat.Parse(Resources.bookmarks_required_placeholder);

    private readonly List<string> _placeholderNames;

    private readonly string _bookmark = string.Empty;

    private BookmarkType _bookmarkType;

    // TODO pass in an array of placeholders
    public BookmarkPlaceholderForm(string name, string url, BookmarkType bookmarkType)
    {
        _bookmark = url;
        _bookmarkType = bookmarkType;

        var r = new Regex(Regex.Escape("{") + "(.*?)" + Regex.Escape("}"));
        var matches = r.Matches(url);
        _placeholderNames = matches.Select(m => m.Groups[1].Value).ToList();
        var inputs = _placeholderNames.Select(p =>
        {
            var errorMessage = string.Format(CultureInfo.CurrentCulture, ErrorMessage, p);
            return $$"""
{
    "type": "Input.Text",
    "style": "text",
    "id": "{{JsonSerializer.Serialize(p, BookmarkSerializationContext.Default.String)}}",
    "label": "{{JsonSerializer.Serialize(p, BookmarkSerializationContext.Default.String)}}",
    "isRequired": true,
    "errorMessage": "{{JsonSerializer.Serialize(errorMessage, BookmarkSerializationContext.Default.String)}}"
}
""";
        }).ToList();

        var allInputs = string.Join(",", inputs);

        TemplateJson = $$"""
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
""" + allInputs + $$"""
  ],
  "actions": [
    {
      "type": "Action.Submit",
      "title": {{JsonSerializer.Serialize(Resources.bookmarks_form_open, BookmarkSerializationContext.Default.String)}},
      "data": {
        "placeholder": "placeholder"
      }
    }
  ]
}
""";
    }

    public override CommandResult SubmitForm(string payload)
    {
        var target = _bookmark;

        // parse the submitted JSON and then open the link
        var formInput = JsonNode.Parse(payload);
        var formObject = formInput?.AsObject();
        if (formObject == null)
        {
            return CommandResult.GoHome();
        }

        foreach (var (key, value) in formObject)
        {
            var placeholderString = $"{{{key}}}";
            var placeholderData = value?.ToString();
            target = target.Replace(placeholderString, placeholderData);
        }

        try
        {
            return ShellCommand.Invoke(_bookmarkType, target);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
        }

        return CommandResult.GoHome();
    }
}
