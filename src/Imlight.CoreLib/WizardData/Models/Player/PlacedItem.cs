/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * HOUSING SYSTEM
 * ========================================================================
 *
 * PURPOSE:
 * One object placed in a house, as a plain record: template, position, angle, scale and look. The
 * client's blob entry is built from it only when a blob is sent.
 *
 * USAGE EXAMPLE:
 * var item = new PlacedItem { TemplateId = id, X = x, Y = y, Z = z, Yaw = yaw }.Normalize();
 * HousingBlobObject entry = item.ToBlobObject();
 *
 * NOTE:
 * - Normalize quantises every value to what the client can show, so what is stored is what every
 *   client shows after a relog. It returns null for a value the client format cannot hold.
 * - The yaw of an exact angle is stored in the middle of its whole degree, because the client
 *   truncates the angle to whole degrees.
 * - ItemGid names the item document of the backpack item the object was placed from. The house holds only that id, as the
 *   backpack and the bank do, so a pet keeps its stats, name and dyes.
 * - A remap index of 0 is a set index, so the remap is null when it is not used.
 * - Custom is the serialized item behavior of a pet or a mount, which its blob entry carries as a custom entry.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System;
using System.Collections.Generic;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.WizardData.Models.Player;

/// <summary>
/// An object placed in a house.
/// </summary>
public sealed record PlacedItem {

    public const int MAX_COORDINATE = 0x7FFF;
    public const float MIN_SCALE = 0.10f;
    public const float MAX_SCALE = 4.0f;
    public const int MAX_BRIGHTNESS = 100;
    public const int MAX_TINT = 15;
    public const int MAX_REMAP_INDEX = 15;

    private const uint TEMPLATE_ID_MASK = 0x0FFFFFFF;
    private const uint REMAP_FLAG = 0x20000000;
    private const uint EXTENDED_YAW_FLAG = 0x80000000;
    private const uint EXTENDED_YAW_OVER255_FLAG = 0x40000000;
    private const uint TINT_SHIFT = 3;
    private const uint SCALE_SHIFT = 8;
    private const uint EXTENDED_YAW_SHIFT = 16;
    private const uint EXTENDED_YAW_MASK = 0xFF;
    private const uint BRIGHTNESS_SHIFT = 24;
    private const uint BRIGHTNESS_MASK = 0x7F;
    private const uint REMAP_MASK = 0xF;
    private const uint TILE_REMAP_SHIFT = 4;
    private const uint NEGATIVE_COORDINATE_BIT = 0x8000;
    private const byte FIRST_FINE_SCALE_CODE = 0x1A;
    private const byte FIRST_QUARTER_SCALE_CODE = 0x1D;
    private const byte LAST_QUARTER_SCALE_CODE = 0x2C;
    private const int DEGREES_PER_SECTOR = 45;
    private const int DEGREES_PER_TURN = 360;
    private const float TWO_PI = 6.2831855f;
    private const float PI = 3.1415927f;
    private const float EIGHTH_TURN = 0.7853982f;

    // The sector codes are not in compass order: the code of 45 degrees is 4, of 90 degrees 1, and so on.
    private static readonly byte[] s_sectorByDirection = [0, 4, 1, 5, 2, 6, 3, 7];
    private static readonly float[] s_sectorThresholds = [
        0.3926991f, 1.1780972f, 1.9634954f, 2.7488935f, 3.534292f, 4.3196898f, 5.105088f, 5.8904867f
    ];
    private static readonly (byte Code, float Value)[] s_scaleGrid = BuildScaleGrid();

    /// <summary>
    /// The id of the item inside its house. It is never reused, so it also orders the items.
    /// </summary>
    public uint Id { get; init; }

    /// <summary>
    /// The global id of the item document this was placed from, or 0 for an object a layout placed. The document stays
    /// in the items collection while the object is placed, so a pick up gives back the same item.
    /// </summary>
    public ulong ItemGid { get; init; }

    public uint TemplateId { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }

    /// <summary>
    /// The angle in radians.
    /// </summary>
    public float Yaw { get; init; }

    /// <summary>
    /// Whether the angle is kept in whole degrees instead of the eight compass directions.
    /// </summary>
    public bool ExactYaw { get; init; }

    public float Scale { get; init; } = 1.0f;
    public int Brightness { get; init; } = MAX_BRIGHTNESS;
    public int Tint { get; init; }
    public int? WallpaperRemap { get; init; }
    public int? TileRemap { get; init; }

    /// <summary>
    /// The serialized item behavior a pet's or mount's blob entry carries, or <c>null</c> for a plain object.
    /// </summary>
    public byte[] Custom { get; init; }

    /// <summary>
    /// Creates the placed item of a layout object.
    /// </summary>
    /// <param name="layoutObject">The object of a <c>HousingLayout</c>.</param>
    /// <returns>The normalized item without an id, or <c>null</c> when a value cannot be stored.</returns>
    public static PlacedItem FromLayout(HousingLayoutObject layoutObject)
        => layoutObject is null
            ? null
            : new PlacedItem {
                TemplateId = layoutObject.m_templateID,
                X = layoutObject.m_positionX,
                Y = layoutObject.m_positionY,
                Z = layoutObject.m_positionZ,
                Yaw = layoutObject.m_yaw,
            }.Normalize();

    /// <summary>
    /// Quantises the values to what the client can show.
    /// </summary>
    /// <returns>
    /// The normalized copy, or <c>null</c> when the template id is 0 or too large, a value is not finite, a coordinate
    /// or the height is beyond the client's range, or the scale, brightness, tint or a remap index is out of range.
    /// </returns>
    public PlacedItem Normalize() {
        if (TemplateId == 0 || TemplateId > TEMPLATE_ID_MASK || !TryRoundCoordinate(X, out var x)
            || !TryRoundCoordinate(Y, out var y) || !float.IsFinite(Z) || MathF.Abs(Z) > MAX_COORDINATE
            || !float.IsFinite(Yaw) || !float.IsFinite(Scale) || Scale < MIN_SCALE || Scale > MAX_SCALE
            || Brightness is < 0 or > MAX_BRIGHTNESS || Tint is < 0 or > MAX_TINT
            || WallpaperRemap is < 0 or > MAX_REMAP_INDEX || TileRemap is < 0 or > MAX_REMAP_INDEX) {
            return null;
        }

        var exactYaw = ExactYaw;
        float yaw;
        var degrees = exactYaw ? ClientDegrees(Yaw) : 0;
        if (exactYaw && degrees % DEGREES_PER_SECTOR != 0) {
            yaw = (degrees + 0.5f) * PI / 180f;
        } else {
            var direction = exactYaw ? degrees / DEGREES_PER_SECTOR : DirectionOf(Yaw);
            yaw = direction * EIGHTH_TURN;
            exactYaw = false;
        }

        return this with { X = x, Y = y, Yaw = yaw, ExactYaw = exactYaw, Scale = SnapScale(Scale) };
    }

    /// <summary>
    /// Packs the item into the client's blob entry. The item should be normalized.
    /// </summary>
    /// <returns>The blob entry.</returns>
    public HousingBlobObject ToBlobObject() {
        var template = TemplateId & TEMPLATE_ID_MASK;
        var extra = 0u;

        var degrees = ExactYaw ? ClientDegrees(Yaw) : 0;
        if (ExactYaw && degrees % DEGREES_PER_SECTOR != 0) {
            template |= EXTENDED_YAW_FLAG;
            if (degrees > byte.MaxValue) {
                template |= EXTENDED_YAW_OVER255_FLAG;
            }

            extra |= ((uint) degrees & EXTENDED_YAW_MASK) << (int) EXTENDED_YAW_SHIFT;
        }

        if (WallpaperRemap is not null || TileRemap is not null) {
            template |= REMAP_FLAG;
            extra |= ((uint) (WallpaperRemap ?? 0) & REMAP_MASK) | (((uint) (TileRemap ?? 0) & REMAP_MASK) << (int) TILE_REMAP_SHIFT);
        }

        extra |= (uint) ScaleCode(Scale) << (int) SCALE_SHIFT;
        if (Brightness != MAX_BRIGHTNESS) {
            extra |= (((uint) Brightness + 1) & BRIGHTNESS_MASK) << (int) BRIGHTNESS_SHIFT;
        }

        var yaw = (byte) (((uint) Tint << (int) TINT_SHIFT) | SectorCode(Yaw));
        if (Custom is not null) {
            return new HousingBlobCustomObject {
                m_gameObjectTemplateID = template,
                m_positionXY = (PackCoordinate(X) << 16) | PackCoordinate(Y),
                m_positionZ = Z,
                m_yaw = yaw,
                m_extraData1 = extra,
                m_customData = Custom,
            };
        }

        return new HousingBlobObject {
            m_gameObjectTemplateID = template,
            m_positionXY = (PackCoordinate(X) << 16) | PackCoordinate(Y),
            m_positionZ = Z,
            m_yaw = yaw,
            m_extraData1 = extra,
        };
    }

    private static bool TryRoundCoordinate(float value, out float rounded) {
        rounded = 0;
        if (!float.IsFinite(value) || MathF.Abs(value) > MAX_COORDINATE + 1) {
            return false;
        }

        var magnitude = value >= 0 ? (int) (value + 0.5f) : (int) (0.5f - value);
        if (magnitude > MAX_COORDINATE) {
            return false;
        }

        rounded = value >= 0 ? magnitude : -magnitude;

        return true;
    }

    private static uint PackCoordinate(float value)
        => value >= 0 ? (uint) (value + 0.5f) & MAX_COORDINATE : NEGATIVE_COORDINATE_BIT + ((uint) (0.5f - value) & MAX_COORDINATE);

    private static float ReduceYaw(float yaw) {
        var reduced = yaw - (float) (int) (yaw / TWO_PI) * TWO_PI;

        return reduced < 0 ? reduced + TWO_PI : reduced;
    }

    private static int ClientDegrees(float yaw)
        => (int) (ReduceYaw(yaw) * 180f / PI) % DEGREES_PER_TURN;

    private static int DirectionOf(float yaw) {
        var reduced = ReduceYaw(yaw);
        for (var direction = 0; direction < s_sectorThresholds.Length; direction++) {
            if (reduced < s_sectorThresholds[direction]) {
                return direction;
            }
        }

        return 0;
    }

    private static byte SectorCode(float yaw) => s_sectorByDirection[DirectionOf(yaw)];

    private static float SnapScale(float scale) => NearestScale(scale).Value;

    private static byte ScaleCode(float scale) => NearestScale(scale).Code;

    private static (byte Code, float Value) NearestScale(float scale) {
        var best = s_scaleGrid[0];
        foreach (var step in s_scaleGrid) {
            if (MathF.Abs(step.Value - scale) < MathF.Abs(best.Value - scale)) {
                best = step;
            }
        }

        return best;
    }

    private static (byte Code, float Value)[] BuildScaleGrid() {
        var grid = new List<(byte, float)> {
            (0, 1.0f),
            (FIRST_FINE_SCALE_CODE, 0.10f),
            (FIRST_FINE_SCALE_CODE + 1, 0.15f),
            (FIRST_FINE_SCALE_CODE + 2, 0.20f),
        };
        for (var code = FIRST_QUARTER_SCALE_CODE; code <= LAST_QUARTER_SCALE_CODE; code++) {
            grid.Add(((byte) code, 0.25f * (code - FIRST_QUARTER_SCALE_CODE + 1)));
        }

        return [.. grid];
    }

}
