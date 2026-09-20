using PDF_Manager.Core.Analysis;
using PDF_Manager.Rendering.Scene;

namespace PDF_Manager.Shell.Viewport;

internal sealed record ViewportSectionForceValue(
    string Id,
    string MemberId,
    string SegmentId,
    string StationId,
    string EndLabel,
    ForceComponents Components);

/// <summary>
/// Applies one deterministic signed extrema policy to viewport glyphs and result-table rows.
/// A row's representative value is the signed component with the greatest absolute value;
/// equal component and row candidates retain contract order.
/// </summary>
internal static class ViewportResultExtrema
{
    internal static IReadOnlyList<NodeDisplacement> SelectDisplacements(
        AnalysisResult? result,
        SceneExtremaMode mode)
    {
        IReadOnlyList<NodeDisplacement> values = result switch
        {
            ForceAnalysisResult force => force.NodeDisplacements,
            ModalAnalysisResult modal => modal.NodeModeShapes,
            _ => [],
        };
        return Select(values, mode, static value => SignedValue(value.Components));
    }

    internal static IReadOnlyList<SupportReaction> SelectReactions(
        AnalysisResult? result,
        SceneExtremaMode mode)
    {
        IReadOnlyList<SupportReaction> values = result is ForceAnalysisResult force
            ? force.SupportReactions
            : [];
        return Select(values, mode, static value => SignedValue(value.Components));
    }

    internal static IReadOnlyList<ViewportSectionForceValue> SelectSectionForces(
        AnalysisResult? result,
        SceneExtremaMode mode)
    {
        if (result is not ForceAnalysisResult force)
        {
            return [];
        }

        List<ViewportSectionForceValue> values = [];
        foreach (MemberSectionForces member in force.MemberSectionForces)
        {
            foreach (MemberSegmentResult segment in member.Segments)
            {
                values.Add(new ViewportSectionForceValue(
                    $"{member.MemberId}/{segment.SegmentId}/i",
                    member.MemberId,
                    segment.SegmentId,
                    segment.StationI,
                    "I",
                    segment.IEnd));
                values.Add(new ViewportSectionForceValue(
                    $"{member.MemberId}/{segment.SegmentId}/j",
                    member.MemberId,
                    segment.SegmentId,
                    segment.StationJ,
                    "J",
                    segment.JEnd));
            }
        }

        return Select(values, mode, static value => SignedValue(value.Components));
    }

    internal static double SignedValue(NodeDisplacement value) => SignedValue(value.Components);

    internal static double SignedValue(SupportReaction value) => SignedValue(value.Components);

    internal static double SignedValue(ViewportSectionForceValue value) => SignedValue(value.Components);

    private static double SignedValue(DisplacementComponents value) => Dominant(
        value.Dx, value.Dy, value.Dz, value.Rx, value.Ry, value.Rz);

    private static double SignedValue(ForceComponents value) => Dominant(
        value.Fx, value.Fy, value.Fz, value.Mx, value.My, value.Mz);

    private static double Dominant(params double[] values)
    {
        double selected = values[0];
        for (int index = 1; index < values.Length; index++)
        {
            if (Math.Abs(values[index]) > Math.Abs(selected))
            {
                selected = values[index];
            }
        }

        return selected;
    }

    private static IReadOnlyList<T> Select<T>(
        IReadOnlyList<T> values,
        SceneExtremaMode mode,
        Func<T, double> selector)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (mode == SceneExtremaMode.Values || values.Count <= 1)
        {
            return values;
        }

        int selectedIndex = 0;
        double selectedValue = selector(values[0]);
        for (int index = 1; index < values.Count; index++)
        {
            double candidate = selector(values[index]);
            bool replace = mode switch
            {
                SceneExtremaMode.Minimum => candidate < selectedValue,
                SceneExtremaMode.Maximum => candidate > selectedValue,
                SceneExtremaMode.AbsoluteMaximum => Math.Abs(candidate) > Math.Abs(selectedValue),
                _ => throw new ArgumentOutOfRangeException(nameof(mode)),
            };
            if (replace)
            {
                selectedIndex = index;
                selectedValue = candidate;
            }
        }

        return [values[selectedIndex]];
    }
}
