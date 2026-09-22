using FrameWebforCS.Core.Analysis;

namespace FrameWebforCS.Core.Results;

internal sealed record PickupEngineeringSource(
    string Id,
    double Factor,
    IReadOnlyList<MemberSectionForces> MemberSectionForces);

internal static class PickupEngineeringEnvelopeBuilder
{
    internal static PickupEngineeringEnvelope Build(
        string pickupId,
        IReadOnlyList<PickupEngineeringSource> sources)
    {
        IReadOnlyList<MemberSectionForces> template = sources[0].MemberSectionForces;
        List<PickupMemberEndEnvelope> memberEnds = [];
        for (int memberIndex = 0; memberIndex < template.Count; memberIndex++)
        {
            MemberSectionForces member = template[memberIndex];
            double memberDistance = 0;
            for (int segmentIndex = 0; segmentIndex < member.Segments.Count; segmentIndex++)
            {
                MemberSegmentResult segment = member.Segments[segmentIndex];
                memberEnds.Add(BuildMemberEnd(
                    member.MemberId,
                    segment,
                    MemberForceEnd.I,
                    segment.StationI,
                    memberDistance,
                    sources,
                    source => source.MemberSectionForces[memberIndex].Segments[segmentIndex].IEnd));
                memberDistance += segment.Length;
                if (!double.IsFinite(memberDistance))
                {
                    throw new ResultPresentationException(
                        ResultPresentationErrorCode.ArithmeticOverflow,
                        "PICKUP member distance produced a non-finite value.");
                }

                memberEnds.Add(BuildMemberEnd(
                    member.MemberId,
                    segment,
                    MemberForceEnd.J,
                    segment.StationJ,
                    memberDistance,
                    sources,
                    source => source.MemberSectionForces[memberIndex].Segments[segmentIndex].JEnd));
            }
        }

        return new PickupEngineeringEnvelope(pickupId, memberEnds);
    }

    private static PickupMemberEndEnvelope BuildMemberEnd(
        string memberId,
        MemberSegmentResult segment,
        MemberForceEnd end,
        string stationId,
        double distance,
        IReadOnlyList<PickupEngineeringSource> sources,
        Func<PickupEngineeringSource, ForceComponents> selector)
    {
        PickupForceWinner[] weighted = sources
            .Select(source => new PickupForceWinner(
                source.Id,
                Scale(selector(source), source.Factor)))
            .ToArray();
        return new PickupMemberEndEnvelope(
            memberId,
            segment.SegmentId,
            stationId,
            end,
            distance,
            segment.Length,
            Enum.GetValues<PickupFocusComponent>().Select(focus => BuildComponent(focus, weighted)));
    }

    private static PickupForceComponentEnvelope BuildComponent(
        PickupFocusComponent focus,
        IReadOnlyList<PickupForceWinner> weighted)
    {
        PickupForceWinner maximum = weighted[0];
        PickupForceWinner minimum = maximum;
        foreach (PickupForceWinner candidate in weighted.Skip(1))
        {
            double candidateValue = GetComponent(candidate.Components, focus);
            if (candidateValue > GetComponent(maximum.Components, focus))
            {
                maximum = candidate;
            }

            if (candidateValue < GetComponent(minimum.Components, focus))
            {
                minimum = candidate;
            }
        }

        return new PickupForceComponentEnvelope(focus, maximum, minimum);
    }

    private static ForceComponents Scale(ForceComponents value, double factor)
    {
        ForceComponents scaled = new(
            factor * value.Fx,
            factor * value.Fy,
            factor * value.Fz,
            factor * value.Mx,
            factor * value.My,
            factor * value.Mz);
        if (!AllFinite(scaled))
        {
            throw new ResultPresentationException(
                ResultPresentationErrorCode.ArithmeticOverflow,
                "Derived result arithmetic produced a non-finite value.");
        }

        return scaled;
    }

    private static bool AllFinite(ForceComponents value)
        => double.IsFinite(value.Fx) && double.IsFinite(value.Fy) &&
            double.IsFinite(value.Fz) && double.IsFinite(value.Mx) &&
            double.IsFinite(value.My) && double.IsFinite(value.Mz);

    private static double GetComponent(ForceComponents value, PickupFocusComponent focus)
        => focus switch
        {
            PickupFocusComponent.Fx => value.Fx,
            PickupFocusComponent.Fy => value.Fy,
            PickupFocusComponent.Fz => value.Fz,
            PickupFocusComponent.Mx => value.Mx,
            PickupFocusComponent.My => value.My,
            PickupFocusComponent.Mz => value.Mz,
            _ => throw new ArgumentOutOfRangeException(nameof(focus)),
        };
}
