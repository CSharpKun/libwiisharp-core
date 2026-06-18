/* This file is part of LibWiiSharpCore
 * Copyright (C) 2009 Leathl
 * Copyright (C) 2020 - 2022 TheShadowEevee, Github Contributors
 * Copyright (C) 2026 CSharpKun
 *
 * LibWiiSharpCore is free software: you can redistribute it and/or
 * modify it under the terms of the GNU General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * LibWiiSharpCore is distributed in the hope that it will be
 * useful, but WITHOUT ANY WARRANTY; without even the implied warranty
 * of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

namespace LibWiiSharpCore;

public enum BrlType
{
    Brlan,
    Brlyt,
}

public static class Brl
{
    #region Public Functions
    /// <summary>
    /// Gets all TPLs that are required by the brlxx (Frame Animation).
    /// </summary>
    /// <param name="pathToBrl"></param>
    /// <returns></returns>
    public static string[] GetBrlTpls(string pathToBrl, BrlType brlType)
    {
        return GetBrlTpls(File.ReadAllBytes(pathToBrl), brlType);
    }

    /// <summary>
    /// Gets all TPLs that are required by the brlxx (Frame Animation).
    /// </summary>
    /// <param name="brlFile"></param>
    /// <returns></returns>
    public static string[] GetBrlTpls(byte[] brlFile, BrlType brlType)
    {
        List<string> stringList = [];
        var offset = GetOffset(brlType);
        int numOfTpls = GetNumOfTpls(brlFile, offset);
        int index1 = GetTplIndex(brlFile, offset);
        for (int index2 = 0; index2 < numOfTpls; ++index2)
        {
            string empty = string.Empty;
            while (brlFile[index1] != 0)
            {
                empty += Convert.ToChar(brlFile[index1++]).ToString();
            }

            stringList.Add(empty);
            ++index1;
        }
        stringList =
        [
            .. stringList.Where(static s => s.EndsWith(".tpl", StringComparison.OrdinalIgnoreCase)),
        ];
        return [.. stringList];
    }

    /// <summary>
    /// Gets all TPLs that are required by the brlxx (Frame Animation).
    /// </summary>
    /// <param name="wad"></param>
    /// <param name="banner"></param>
    /// <returns></returns>
    public static string[] GetBrlTpls(WAD wad, bool banner, BrlType brlType)
    {
        if (!wad.HasBanner)
            return [];

        string str = nameof(banner);
        if (!banner)
        {
            str = "icon";
        }

        for (int index1 = 0; index1 < wad.BannerApp.Nodes.Count; ++index1)
        {
            if (
                wad
                    .BannerApp.StringTable[index1]
                    .Equals(str + ".bin", StringComparison.OrdinalIgnoreCase)
            )
                continue;

            var u8 = U8.Load(wad.BannerApp.Data[index1]);

            string[] pattern = brlType switch
            {
                BrlType.Brlan => [str + "_start.brlan", str + "_loop.brlan", str + ".brlan"],
                BrlType.Brlyt => [str + ".brlyt"],
                _ => throw new ArgumentOutOfRangeException(
                    nameof(brlType),
                    brlType,
                    "Unexpected type of brl file"
                ),
            };

            return
            [
                .. u8
                    .Nodes.Where(
                        (_, i) =>
                            pattern.Any(pattern =>
                                u8.StringTable[i]
                                    .Equals(pattern, StringComparison.OrdinalIgnoreCase)
                            )
                    )
                    .SelectMany((_, i) => GetBrlTpls(u8.Data[i], brlType)),
            ];
        }
        return [];
    }
    #endregion

    private static int GetNumOfTpls(byte[] brlFile, BrlOffset offset) =>
        Shared.Swap(BitConverter.ToUInt16(brlFile, offset.StartIndex));

    private static int GetTplIndex(byte[] brlFile, BrlOffset offset) =>
        offset.Offset + GetNumOfTpls(brlFile, offset) * offset.Multiplier;

    private static BrlOffset GetOffset(BrlType brlType) =>
        brlType switch
        {
            BrlType.Brlan => new(28, 36, 4),
            BrlType.Brlyt => new(44, 48, 8),
            _ => throw new ArgumentOutOfRangeException(
                nameof(brlType),
                brlType,
                "Unexpected type of brl file"
            ),
        };
}

public record struct BrlOffset(int StartIndex, int Offset, int Multiplier);
