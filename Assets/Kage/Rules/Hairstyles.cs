using System;
using System.Collections.Generic;

namespace Kage.Rules
{
    public static class Hairstyles
    {
        public const string Default = "spike";
        public static readonly IReadOnlyList<string> Ids = Array.AsReadOnly(new[]
        {
            Default, "messy_layers", "low_ponytail", "topknot", "long_flowing", "undercut",
        });

        public static string Name(string id) => id switch
        {
            Default => "Spike",
            "messy_layers" => "Messy Layers",
            "low_ponytail" => "Low Ponytail",
            "topknot" => "Topknot (Samurai)",
            "long_flowing" => "Long Flowing",
            "undercut" => "Undercut",
            _ => null,
        };

        public static string Resolve(string id) => Name(id) == null ? Default : id;
    }
}
