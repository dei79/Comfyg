﻿using Comfyg.Client;
using Comfyg.Store.Contracts;

namespace Comfyg.Cli.Commands.Import;

internal class ImportSettingsCommand : ImportCommandBase<ISettingValue>
{
    public ImportSettingsCommand()
        : base("settings", "Imports key-value pairs as setting values into the connected Comfyg store.")
    {
    }

    protected override IEnumerable<ISettingValue> BuildAddValuesRequest(IEnumerable<KeyValuePair<string, string>> kvp)
    {
        return kvp.Select(i => new SettingValue(i.Key, i.Value));
    }
}
