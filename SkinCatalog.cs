using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SkinGroups = System.Collections.Generic.SortedDictionary<string, System.Collections.Generic.SortedSet<ulong>>;

namespace ApprovedSkinExport
{
    internal sealed class SkinCatalog
    {
        private sealed class SkinEntry
        {
            internal readonly string Item;
            internal readonly ulong WorkshopId;
            internal readonly ulong InventoryId;
            internal readonly string Name;

            internal SkinEntry(string item, ulong workshopId, ulong inventoryId, string name)
            {
                Item = item;
                WorkshopId = workshopId;
                InventoryId = inventoryId;
                Name = name;
            }
        }

        private readonly SkinGroups _workshop = new SkinGroups(StringComparer.Ordinal);
        private readonly SkinGroups _inventory = new SkinGroups(StringComparer.Ordinal);
        private readonly List<SkinEntry> _entries = new List<SkinEntry>();

        private int _missingWorkshopCount;
        private int _missingItemCount;

        internal void AddSkin(string item, ulong workshopId, ulong inventoryId, string name)
        {
            if (String.IsNullOrWhiteSpace(item))
            {
                item = "unmapped." + FormatId(inventoryId);
                ++_missingItemCount;
            }

            AddSkinId(_inventory, item, inventoryId);
            if (workshopId != 0)
            {
                AddSkinId(_workshop, item, workshopId);
            }
            else
            {
                ++_missingWorkshopCount;
            }

            _entries.Add(new SkinEntry(item, workshopId, inventoryId, name));
        }

        internal void WriteFiles(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            _entries.Sort(CompareEntries);

            WriteFileAtomically(Path.Combine(outputDirectory, "skins-workshop.txt"), FormatGroups(_workshop));
            WriteFileAtomically(Path.Combine(outputDirectory, "skins-inventory.txt"), FormatGroups(_inventory));
            WriteFileAtomically(Path.Combine(outputDirectory, "skins-details.tsv"), FormatDetails());
        }

        internal string GetExportSummary(string outputDirectory)
        {
            return String.Format(CultureInfo.InvariantCulture,
                "Exported {0} Approved rows; {1} Workshop item groups; " +
                "{2} rows without Workshop ID; {3} unmapped rows. Output: {4}",
                _entries.Count, _workshop.Count, _missingWorkshopCount, _missingItemCount, outputDirectory);
        }

        private static void AddSkinId(SkinGroups groups, string item, ulong id)
        {
            SortedSet<ulong> ids;

            if (!groups.TryGetValue(item, out ids))
            {
                ids = new SortedSet<ulong>();
                groups.Add(item, ids);
            }

            ids.Add(id);
        }

        private static int CompareEntries(SkinEntry left, SkinEntry right)
        {
            int result = StringComparer.Ordinal.Compare(left.Item, right.Item);

            if (result == 0)
            {
                result = left.WorkshopId.CompareTo(right.WorkshopId);
            }

            if (result == 0)
            {
                result = left.InventoryId.CompareTo(right.InventoryId);
            }

            return result;
        }

        private static List<string> FormatGroups(SkinGroups groups)
        {
            List<string> lines = new List<string>(groups.Count);

            foreach (KeyValuePair<string, SortedSet<ulong>> group in groups)
            {
                StringBuilder line = new StringBuilder();
                line.Append(group.Key);
                line.Append(": ");

                bool first = true;

                foreach (ulong id in group.Value)
                {
                    if (!first)
                    {
                        line.Append(',');
                    }

                    line.Append(FormatId(id));
                    first = false;
                }

                lines.Add(line.ToString());
            }

            return lines;
        }

        private List<string> FormatDetails()
        {
            List<string> lines = new List<string>(_entries.Count + 1);
            lines.Add("item\tworkshop_id\tinventory_id\tname");

            foreach (SkinEntry entry in _entries)
            {
                lines.Add(String.Concat(
                    SanitizeText(entry.Item), "\t", FormatId(entry.WorkshopId), "\t",
                    FormatId(entry.InventoryId), "\t", SanitizeText(entry.Name)));
            }

            return lines;
        }

        private static void WriteFileAtomically(string path, List<string> lines)
        {
            string temporaryPath = path + ".tmp";

            try
            {
                using (StreamWriter writer = new StreamWriter(temporaryPath, false, new UTF8Encoding(false)))
                {
                    foreach (string line in lines)
                    {
                        writer.WriteLine(line);
                    }
                }
                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, null);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static string FormatId(ulong id)
        {
            return id.ToString(CultureInfo.InvariantCulture);
        }

        private static string SanitizeText(string value)
        {
            if (value == null)
            {
                return String.Empty;
            }

            return value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }
    }
}
