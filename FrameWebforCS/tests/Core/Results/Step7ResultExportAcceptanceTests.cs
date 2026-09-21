using System.Globalization;
using System.Text;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Results;

namespace PDF_Manager.Core.Tests.Results;

public sealed class Step7ResultExportAcceptanceTests
{
    public static TheoryData<string, string> SpreadsheetDangerousCaseIds => new()
    {
        { "=case", "'=case" },
        { "+case", "'+case" },
        { "-case", "'-case" },
        { "@case", "'@case" },
        { "\tcase", "'\tcase" },
        { "\rcase", "\"'\rcase\"" },
        { "\ncase", "\"'\ncase\"" },
        { "  =case", "'  =case" },
    };

    [Fact]
    public async Task BaseStaticCsv_IsExactInvariantUtf8Rfc4180WithCrLfAndNoBom()
    {
        const string caseId = "Case,\"α\r\nLine";
        const string nodeId = "N,\"α\r\n1";
        StaticAnalysisResult result = CreateNodeOnlyStatic(caseId, nodeId);
        ResultCsvExporter exporter = new();
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ja-JP");

            ResultCsvExport export = exporter.ExportBaseStatic(result);

            const string quotedCase = "\"Case,\"\"α\r\nLine\"";
            const string quotedNode = "\"N,\"\"α\r\n1\"";
            string expected =
                ResultCsvExporter.Header + "\r\n" +
                $"{quotedCase},base_static,{quotedCase},static,0,node_displacement,{quotedNode},,,dx,value,1.25,{quotedCase}\r\n" +
                $"{quotedCase},base_static,{quotedCase},static,0,node_displacement,{quotedNode},,,dy,value,-2.5,{quotedCase}\r\n" +
                $"{quotedCase},base_static,{quotedCase},static,0,node_displacement,{quotedNode},,,dz,value,3,{quotedCase}\r\n" +
                $"{quotedCase},base_static,{quotedCase},static,0,node_displacement,{quotedNode},,,rx,value,0.0001,{quotedCase}\r\n" +
                $"{quotedCase},base_static,{quotedCase},static,0,node_displacement,{quotedNode},,,ry,value,-0.0002,{quotedCase}\r\n" +
                $"{quotedCase},base_static,{quotedCase},static,0,node_displacement,{quotedNode},,,rz,value,1.2345678901234567,{quotedCase}\r\n";
            byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
            byte[] actualBytes = export.Utf8Bytes.ToArray();

            Assert.Equal(expected, export.Text);
            Assert.Equal(expectedBytes, actualBytes);
            Assert.False(actualBytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
            Assert.Equal(6, export.RowCount);
            Assert.Equal(expectedBytes.Length, export.ByteCount);
            Assert.Equal("text/csv; charset=utf-8", export.ContentType);
            Assert.Equal("Case__α__Line-base_static.csv", export.SuggestedFileName);
            Assert.EndsWith("\r\n", export.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("\n", export.Text.Replace("\r\n", string.Empty, StringComparison.Ordinal));

            using MemoryStream sync = new();
            export.WriteTo(sync);
            Assert.Equal(expectedBytes, sync.ToArray());
            using MemoryStream async = new();
            await export.WriteToAsync(async);
            Assert.Equal(expectedBytes, async.ToArray());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Theory]
    [MemberData(nameof(SpreadsheetDangerousCaseIds))]
    public void BaseStaticCsv_NeutralizesSpreadsheetTextButKeepsNegativeNumbersNumeric(
        string caseId,
        string expectedCaseCell)
    {
        StaticAnalysisResult result = CreateNodeOnlyStatic(caseId, "@node");

        ResultCsvExport export = new ResultCsvExporter().ExportBaseStatic(result);

        string expected =
            ResultCsvExporter.Header + "\r\n" +
            $"{expectedCaseCell},base_static,{expectedCaseCell},static,0,node_displacement,'@node,,,dx,value,1.25,{expectedCaseCell}\r\n" +
            $"{expectedCaseCell},base_static,{expectedCaseCell},static,0,node_displacement,'@node,,,dy,value,-2.5,{expectedCaseCell}\r\n" +
            $"{expectedCaseCell},base_static,{expectedCaseCell},static,0,node_displacement,'@node,,,dz,value,3,{expectedCaseCell}\r\n" +
            $"{expectedCaseCell},base_static,{expectedCaseCell},static,0,node_displacement,'@node,,,rx,value,0.0001,{expectedCaseCell}\r\n" +
            $"{expectedCaseCell},base_static,{expectedCaseCell},static,0,node_displacement,'@node,,,ry,value,-0.0002,{expectedCaseCell}\r\n" +
            $"{expectedCaseCell},base_static,{expectedCaseCell},static,0,node_displacement,'@node,,,rz,value,1.2345678901234567,{expectedCaseCell}\r\n";
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);

        Assert.Equal(expected, export.Text);
        Assert.Equal(expectedBytes, export.Utf8Bytes.ToArray());
        Assert.Contains(",dy,value,-2.5,", export.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(",dy,value,'-2.5,", export.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void PickupEngineeringExports_AreBuiltFromServiceWithExactProvenanceVectorsAndStableTies()
    {
        AnalysisResultSet resultSet = CreatePickupResultSet();
        PresentedStaticResult pickup = Assert.Single(new ResultPresentationService().BuildDerivedResults(
            resultSet,
            [new DerivedResultDefinition(
                "=PICK",
                "Pickup",
                DerivedResultKind.Pickup,
                [new DerivedResultTerm("@A", 1), new DerivedResultTerm("-B", 1)])]));
        Assert.NotNull(pickup.PickupEnvelope);
        Assert.Equal(["@A", "-B"], pickup.SourceIds);
        Assert.Equal([MemberForceEnd.I, MemberForceEnd.J], pickup.PickupEnvelope.MemberEnds.Select(row => row.End));

        ResultCsvExporter exporter = new();
        ResultCsvExport export = exporter.ExportPickup(pickup);

        string expected =
            ResultCsvExporter.PickupHeader + "\r\n" +
            PickupRow("fx", "'@A", "'-B", "S0", "I", 0,
                new ForceComponents(10, -20, 30, -40, 50, -60),
                new ForceComponents(-11, 21, -31, 41, -51, 61)) +
            PickupRow("fx", "'@A", "'@A", "S1", "J", 1,
                new ForceComponents(1, 2, 3, 4, 5, 6),
                new ForceComponents(1, 2, 3, 4, 5, 6)) +
            PickupRow("fy", "'-B", "'@A", "S0", "I", 0,
                new ForceComponents(-11, 21, -31, 41, -51, 61),
                new ForceComponents(10, -20, 30, -40, 50, -60)) +
            PickupRow("fy", "'@A", "'-B", "S1", "J", 1,
                new ForceComponents(1, 2, 3, 4, 5, 6),
                new ForceComponents(1, -2, -3, -4, -5, -6)) +
            PickupRow("fz", "'@A", "'-B", "S0", "I", 0,
                new ForceComponents(10, -20, 30, -40, 50, -60),
                new ForceComponents(-11, 21, -31, 41, -51, 61)) +
            PickupRow("fz", "'@A", "'-B", "S1", "J", 1,
                new ForceComponents(1, 2, 3, 4, 5, 6),
                new ForceComponents(1, -2, -3, -4, -5, -6)) +
            PickupRow("mx", "'-B", "'@A", "S0", "I", 0,
                new ForceComponents(-11, 21, -31, 41, -51, 61),
                new ForceComponents(10, -20, 30, -40, 50, -60)) +
            PickupRow("mx", "'@A", "'-B", "S1", "J", 1,
                new ForceComponents(1, 2, 3, 4, 5, 6),
                new ForceComponents(1, -2, -3, -4, -5, -6)) +
            PickupRow("my", "'@A", "'-B", "S0", "I", 0,
                new ForceComponents(10, -20, 30, -40, 50, -60),
                new ForceComponents(-11, 21, -31, 41, -51, 61)) +
            PickupRow("my", "'@A", "'-B", "S1", "J", 1,
                new ForceComponents(1, 2, 3, 4, 5, 6),
                new ForceComponents(1, -2, -3, -4, -5, -6)) +
            PickupRow("mz", "'-B", "'@A", "S0", "I", 0,
                new ForceComponents(-11, 21, -31, 41, -51, 61),
                new ForceComponents(10, -20, 30, -40, 50, -60)) +
            PickupRow("mz", "'@A", "'-B", "S1", "J", 1,
                new ForceComponents(1, 2, 3, 4, 5, 6),
                new ForceComponents(1, -2, -3, -4, -5, -6));
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        Assert.Equal(expected, export.Text);
        Assert.Equal(expectedBytes, export.Utf8Bytes.ToArray());
        Assert.Equal("_PICK-pickup-3d.csv", export.SuggestedFileName);
        Assert.Equal(12, export.RowCount);
        Assert.DoesNotContain(",disg,", export.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(",reac,", export.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(",fsec,", export.Text, StringComparison.OrdinalIgnoreCase);

        const long pickupWork = 12 * 21L;
        Assert.Equal(expectedBytes, exporter.ExportPickup(
            pickup,
            new ResultCsvExportLimits(12, expectedBytes.Length, pickupWork)).Utf8Bytes.ToArray());
        Assert.Equal(ResultExportLimitKind.Rows, Assert.Throws<ResultExportLimitException>(() =>
            exporter.ExportPickup(pickup, new ResultCsvExportLimits(11, expectedBytes.Length, pickupWork))).LimitKind);
        Assert.Equal(ResultExportLimitKind.Work, Assert.Throws<ResultExportLimitException>(() =>
            exporter.ExportPickup(pickup, new ResultCsvExportLimits(12, expectedBytes.Length, pickupWork - 1))).LimitKind);
        Assert.Equal(ResultExportLimitKind.Bytes, Assert.Throws<ResultExportLimitException>(() =>
            exporter.ExportPickup(pickup, new ResultCsvExportLimits(12, expectedBytes.Length - 1, pickupWork))).LimitKind);

        ResultPickupFixedWidthExport pickup2D = exporter.ExportPickup2D(pickup);
        string expected2D =
            Pickup2DRow("M", "+10", "-B", "@A", "S0", 0,
                new ForceComponents(-11, 21, -31, 41, -51, 61),
                new ForceComponents(10, -20, 30, -40, 50, -60)) +
            Pickup2DRow("M", "+10", "@A", "-B", "S1", 1,
                new ForceComponents(1, 2, 3, 4, 5, 6),
                new ForceComponents(1, -2, -3, -4, -5, -6)) +
            Pickup2DRow("S", "+10", "-B", "@A", "S0", 0,
                new ForceComponents(-11, 21, -31, 41, -51, 61),
                new ForceComponents(10, -20, 30, -40, 50, -60)) +
            Pickup2DRow("S", "+10", "@A", "-B", "S1", 1,
                new ForceComponents(1, 2, 3, 4, 5, 6),
                new ForceComponents(1, -2, -3, -4, -5, -6)) +
            Pickup2DRow("N", "+10", "@A", "-B", "S0", 0,
                new ForceComponents(10, -20, 30, -40, 50, -60),
                new ForceComponents(-11, 21, -31, 41, -51, 61)) +
            Pickup2DRow("N", "+10", "@A", "@A", "S1", 1,
                new ForceComponents(1, 2, 3, 4, 5, 6),
                new ForceComponents(1, 2, 3, 4, 5, 6));
        Assert.Equal(expected2D, pickup2D.Text);
        Assert.Equal(Encoding.UTF8.GetBytes(expected2D), pickup2D.Utf8Bytes.ToArray());
        Assert.Equal("_PICK-pickup-2d.pik", pickup2D.SuggestedFileName);
        Assert.Equal(6, pickup2D.RowCount);

        PresentedStaticResult notPickup = new(
            "DEFINE",
            "Define",
            DerivedResultKind.Define,
            ["A"],
            [],
            [],
            [],
            [],
            []);
        ResultPresentationException exception = Assert.Throws<ResultPresentationException>(
            () => new ResultCsvExporter().ExportPickup(notPickup));
        Assert.Equal(ResultPresentationErrorCode.InvalidDefinition, exception.Code);
    }

    [Fact]
    public void ExportLimits_AcceptExactRowsBytesAndWorkThenRejectPlusOneDeterministically()
    {
        StaticAnalysisResult result = CreateNodeOnlyStatic("C1", "N1");
        ResultCsvExporter exporter = new();
        ResultCsvExport baseline = exporter.ExportBaseStatic(result);
        const int rowCount = 6;
        const long work = rowCount * 13L;

        ResultCsvExport exact = exporter.ExportBaseStatic(
            result,
            new ResultCsvExportLimits(rowCount, baseline.ByteCount, work));
        Assert.Equal(rowCount, exact.RowCount);
        Assert.Equal(baseline.ByteCount, exact.ByteCount);

        AssertLimit(
            Assert.Throws<ResultExportLimitException>(() => exporter.ExportBaseStatic(
                result,
                new ResultCsvExportLimits(rowCount - 1, baseline.ByteCount, work))),
            ResultExportLimitKind.Rows,
            rowCount - 1,
            rowCount);
        AssertLimit(
            Assert.Throws<ResultExportLimitException>(() => exporter.ExportBaseStatic(
                result,
                new ResultCsvExportLimits(rowCount, baseline.ByteCount, work - 1))),
            ResultExportLimitKind.Work,
            work - 1,
            work);
        AssertLimit(
            Assert.Throws<ResultExportLimitException>(() => exporter.ExportBaseStatic(
                result,
                new ResultCsvExportLimits(rowCount, baseline.ByteCount - 1, work))),
            ResultExportLimitKind.Bytes,
            baseline.ByteCount - 1,
            baseline.ByteCount);

        ResultExportLimitException rowsFirst = Assert.Throws<ResultExportLimitException>(
            () => exporter.ExportBaseStatic(result, new ResultCsvExportLimits(1, 1, 1)));
        Assert.Equal(ResultExportLimitKind.Rows, rowsFirst.LimitKind);
        ResultExportLimitException workSecond = Assert.Throws<ResultExportLimitException>(
            () => exporter.ExportBaseStatic(result, new ResultCsvExportLimits(rowCount, 1, work - 1)));
        Assert.Equal(ResultExportLimitKind.Work, workSecond.LimitKind);
    }

    [Fact]
    public void MixedBaseAndMovingCsv_AreExactOrderedGoldensWithNonLexicalIdsAndBoundaryLimits()
    {
        StaticAnalysisResult mixed = CreateMixedStaticResult();
        ResultCsvExporter exporter = new();
        ResultCsvExport baseExport = exporter.ExportBaseStatic(mixed);
        string baseExpected = ResultCsvExporter.Header + "\r\n" +
            BaseRows("C10", "node_displacement", "10", "", "", DisplacementValues(new(1, 2, 3, 4, 5, 6))) +
            BaseRows("C10", "node_displacement", "2", "", "", DisplacementValues(new(-1, -2, -3, -4, -5, -6))) +
            BaseRows("C10", "support_reaction", "2", "", "", NamedForceValues(new(7, 8, 9, 10, 11, 12))) +
            BaseRows("C10", "member_section_force", "10", "S10", "I", NamedForceValues(new(13, 14, 15, 16, 17, 18))) +
            BaseRows("C10", "member_section_force", "10", "S10", "J", NamedForceValues(new(19, 20, 21, 22, 23, 24))) +
            BaseRows("C10", "shell_result", "2", "L10", "membrane", [("nx", 25), ("ny", 26), ("nxy", 27)]) +
            BaseRows("C10", "shell_result", "2", "L10", "bending", [("mx", 28), ("my", 29), ("mxy", 30)]) +
            BaseRows("C10", "shell_result", "2", "L10", "shear", [("qx", 31), ("qy", 32)]) +
            BaseRows("C10", "shell_result", "2", "L10", "top_stress", [("sx", 33), ("sy", 34), ("txy", 35)]) +
            BaseRows("C10", "shell_result", "2", "L10", "bottom_stress", [("sx", 36), ("sy", 37), ("txy", 38)]) +
            BaseRows("C10", "solid_result", "11", "P2", "stress", [("sx", 39), ("sy", 40), ("sz", 41), ("txy", 42), ("tyz", 43), ("tzx", 44)]) +
            BaseRows("C10", "solid_result", "11", "P2", "strain", [("ex", 45), ("ey", 46), ("ez", 47), ("gxy", 48), ("gyz", 49), ("gzx", 50)]);
        AssertExactAndBoundaries(baseExport, baseExpected, 56,
            limits => exporter.ExportBaseStatic(mixed, limits));
        Assert.True(baseExpected.IndexOf(",node_displacement,10,", StringComparison.Ordinal) <
            baseExpected.IndexOf(",node_displacement,2,", StringComparison.Ordinal));

        MovingLoadEnvelope moving = CreateMixedMovingEnvelope();
        ResultCsvExport movingExport = exporter.ExportMovingLoad(moving);
        ScalarEnvelope scalar = GoldenScalar();
        string movingExpected = ResultCsvExporter.Header + "\r\n" +
            MovingComponentRows("\tMOVE", "node_displacement", "\r10", "", "", DisplacementEnvelopeValues(scalar)) +
            MovingComponentRows("\tMOVE", "support_reaction", "+2", "", "", ForceEnvelopeValues(scalar)) +
            MovingComponentRows("\tMOVE", "member_section_force", "-10", "  =S", "I", ForceEnvelopeValues(scalar)) +
            MovingComponentRows("\tMOVE", "member_section_force", "-10", "  =S", "J", ForceEnvelopeValues(scalar)) +
            MovingGlobalRows("\tMOVE", GoldenMemberExtrema());
        AssertExactAndBoundaries(movingExport, movingExpected, 90,
            limits => exporter.ExportMovingLoad(moving, limits));
        Assert.Contains("'\tMOVE", movingExport.Text, StringComparison.Ordinal);
        Assert.Contains("\"'\r10\"", movingExport.Text, StringComparison.Ordinal);
        Assert.Contains("'  =S", movingExport.Text, StringComparison.Ordinal);
        Assert.Contains("\"'\nA\"", movingExport.Text, StringComparison.Ordinal);
        Assert.Contains(",minimum,-2,'@B\r\n", movingExport.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(",minimum,'-2,", movingExport.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void PickupCsv_NeutralizesEveryUntrustedTextColumnBeforeRfc4180Escaping()
    {
        ForceComponents maximum = new(1, -2, 3, -4, 5, -6);
        ForceComponents minimum = new(-1, 2, -3, 4, -5, 6);
        PickupForceComponentEnvelope[] components = Enum.GetValues<PickupFocusComponent>()
            .Select(focus => new PickupForceComponentEnvelope(
                focus,
                new PickupForceWinner("\rMAX", maximum),
                new PickupForceWinner("  @MIN", minimum)))
            .ToArray();
        PickupEngineeringEnvelope envelope = new(
            "=PICK",
            [new PickupMemberEndEnvelope("+MEM", "unused", "\nST", MemberForceEnd.I, 0, 1, components)]);
        PresentedStaticResult pickup = new(
            "=PICK", "Pickup", DerivedResultKind.Pickup, [], [], [], [], [], [], envelope);

        ResultCsvExport export = new ResultCsvExporter().ExportPickup(pickup);
        string firstRow = export.Text.Split("\r\n", StringSplitOptions.None)[1];
        const string expected =
            "'=PICK,fx,'+MEM,\"'\rMAX\",'  @MIN,\"'\nST\",I,0,1,1,-2,3,-4,5,-6,-1,2,-3,4,-5,6";
        Assert.Equal(expected, firstRow);
        Assert.Equal(Encoding.UTF8.GetBytes(export.Text), export.Utf8Bytes.ToArray());
        Assert.DoesNotContain(",'-2,", firstRow, StringComparison.Ordinal);
        Assert.DoesNotContain(",'-4,", firstRow, StringComparison.Ordinal);
        Assert.DoesNotContain(",'-6,", firstRow, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportLimitConfiguration_AcceptsHardCapsAndRejectsEveryHardCapPlusOne()
    {
        ResultCsvExportLimits limits = new(
            ResultCsvExportLimits.HardMaxRows,
            ResultCsvExportLimits.HardMaxBytes,
            ResultCsvExportLimits.HardMaxWork);

        Assert.Equal(ResultCsvExportLimits.HardMaxRows, limits.MaxRows);
        Assert.Equal(ResultCsvExportLimits.HardMaxBytes, limits.MaxBytes);
        Assert.Equal(ResultCsvExportLimits.HardMaxWork, limits.MaxWork);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResultCsvExportLimits(
            ResultCsvExportLimits.HardMaxRows + 1,
            ResultCsvExportLimits.HardMaxBytes,
            ResultCsvExportLimits.HardMaxWork));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResultCsvExportLimits(
            ResultCsvExportLimits.HardMaxRows,
            ResultCsvExportLimits.HardMaxBytes + 1,
            ResultCsvExportLimits.HardMaxWork));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResultCsvExportLimits(
            ResultCsvExportLimits.HardMaxRows,
            ResultCsvExportLimits.HardMaxBytes,
            ResultCsvExportLimits.HardMaxWork + 1));
    }

    private static void AssertLimit(
        ResultExportLimitException exception,
        ResultExportLimitKind expectedKind,
        long expectedLimit,
        long expectedActual)
    {
        Assert.Equal(expectedKind, exception.LimitKind);
        Assert.Equal(expectedLimit, exception.Limit);
        Assert.Equal(expectedActual, exception.Actual);
        Assert.Equal(ResultPresentationErrorCode.ExportLimitExceeded, exception.Code);
        Assert.Equal("ResultExportLimitExceeded", exception.ResourceKey);
    }

    private static void AssertExactAndBoundaries(
        ResultCsvExport export,
        string expected,
        int rowCount,
        Func<ResultCsvExportLimits, ResultCsvExport> exportWithLimits)
    {
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        long work = rowCount * 13L;
        Assert.Equal(expected, export.Text);
        Assert.Equal(expectedBytes, export.Utf8Bytes.ToArray());
        Assert.Equal(rowCount, export.RowCount);
        Assert.Equal(expectedBytes.Length, export.ByteCount);
        Assert.Equal(expectedBytes, exportWithLimits(new(rowCount, expectedBytes.Length, work)).Utf8Bytes.ToArray());
        Assert.Equal(ResultExportLimitKind.Rows, Assert.Throws<ResultExportLimitException>(() =>
            exportWithLimits(new(rowCount - 1, expectedBytes.Length, work))).LimitKind);
        Assert.Equal(ResultExportLimitKind.Work, Assert.Throws<ResultExportLimitException>(() =>
            exportWithLimits(new(rowCount, expectedBytes.Length, work - 1))).LimitKind);
        Assert.Equal(ResultExportLimitKind.Bytes, Assert.Throws<ResultExportLimitException>(() =>
            exportWithLimits(new(rowCount, expectedBytes.Length - 1, work))).LimitKind);
    }

    private static StaticAnalysisResult CreateMixedStaticResult() => new(
        "C10",
        [
            new NodeDisplacement("10", new DisplacementComponents(1, 2, 3, 4, 5, 6)),
            new NodeDisplacement("2", new DisplacementComponents(-1, -2, -3, -4, -5, -6)),
        ],
        [new SupportReaction("2", new ForceComponents(7, 8, 9, 10, 11, 12))],
        [new MemberSectionForces(
            "10",
            [new MemberSegmentResult(
                "S10", "A", "B", 1,
                new ForceComponents(13, 14, 15, 16, 17, 18),
                new ForceComponents(19, 20, 21, 22, 23, 24))])],
        [new ShellResult(
            "2",
            [new ShellResultLocation(
                "L10",
                new MembraneForce(25, 26, 27),
                new BendingMoment(28, 29, 30),
                new TransverseShear(31, 32),
                new PlaneStress(33, 34, 35),
                new PlaneStress(36, 37, 38))])],
        [new SolidResult(
            "11",
            [new SolidResultLocation(
                "P2",
                new Stress3D(39, 40, 41, 42, 43, 44),
                new Strain3D(45, 46, 47, 48, 49, 50))])],
        new WarningDiagnostics([]));

    private static MovingLoadEnvelope CreateMixedMovingEnvelope()
    {
        ScalarEnvelope scalar = GoldenScalar();
        ForceEnvelopeComponents force = new(scalar, scalar, scalar, scalar, scalar, scalar);
        return new MovingLoadEnvelope(
            "\tMOVE",
            ["\nA", "@B"],
            [new NodeDisplacementEnvelope(
                "\r10",
                new DisplacementEnvelopeComponents(scalar, scalar, scalar, scalar, scalar, scalar))],
            [new SupportReactionEnvelope("+2", force)],
            [new MemberSectionForceEnvelope(
                "-10",
                [new MemberSegmentEnvelope("  =S", "S0", "S1", 1, force, force)])],
            GoldenMemberExtrema());
    }

    private static ScalarEnvelope GoldenScalar() => new(
        new EnvelopeExtreme(1, "\nA"),
        new EnvelopeExtreme(-2, "@B"),
        new EnvelopeExtreme(-2, "  +ABS"));

    private static MemberForceExtremaComponents GoldenMemberExtrema()
    {
        MemberForceScalarExtrema scalar = new(
            new MemberForceExtreme(1, "\nA", "10", "  =S", MemberForceEnd.I),
            new MemberForceExtreme(-2, "@B", "10", "  =S", MemberForceEnd.J),
            new MemberForceExtreme(-2, "  +ABS", "10", "  =S", MemberForceEnd.J));
        return new(scalar, scalar, scalar, scalar, scalar, scalar);
    }

    private static IReadOnlyList<(string Component, double Value)> DisplacementValues(DisplacementComponents value) =>
        [("dx", value.Dx), ("dy", value.Dy), ("dz", value.Dz), ("rx", value.Rx), ("ry", value.Ry), ("rz", value.Rz)];

    private static IReadOnlyList<(string Component, double Value)> NamedForceValues(ForceComponents value) =>
        [("fx", value.Fx), ("fy", value.Fy), ("fz", value.Fz), ("mx", value.Mx), ("my", value.My), ("mz", value.Mz)];

    private static IReadOnlyList<(string Component, ScalarEnvelope Value)> DisplacementEnvelopeValues(ScalarEnvelope value) =>
        [("dx", value), ("dy", value), ("dz", value), ("rx", value), ("ry", value), ("rz", value)];

    private static IReadOnlyList<(string Component, ScalarEnvelope Value)> ForceEnvelopeValues(ScalarEnvelope value) =>
        [("fx", value), ("fy", value), ("fz", value), ("mx", value), ("my", value), ("mz", value)];

    private static string BaseRows(
        string caseId,
        string table,
        string entityId,
        string locationId,
        string position,
        IReadOnlyList<(string Component, double Value)> values) => string.Concat(values.Select(value =>
            CsvRow(
                TextCell(caseId), "base_static", TextCell(caseId), "static", "0", table,
                TextCell(entityId), TextCell(locationId), position, value.Component, "value",
                Format(value.Value), TextCell(caseId))));

    private static string MovingComponentRows(
        string viewId,
        string table,
        string entityId,
        string locationId,
        string position,
        IReadOnlyList<(string Component, ScalarEnvelope Value)> values) => string.Concat(values.Select(value =>
            MovingScalarRows(viewId, table, entityId, locationId, position, value.Component, value.Value)));

    private static string MovingScalarRows(
        string viewId,
        string table,
        string entityId,
        string locationId,
        string position,
        string component,
        ScalarEnvelope value) =>
        MovingRow(viewId, table, entityId, locationId, position, component, "maximum", value.Maximum) +
        MovingRow(viewId, table, entityId, locationId, position, component, "minimum", value.Minimum) +
        MovingRow(viewId, table, entityId, locationId, position, component, "absolute_maximum", value.AbsoluteMaximum);

    private static string MovingRow(
        string viewId,
        string table,
        string entityId,
        string locationId,
        string position,
        string component,
        string extreme,
        EnvelopeExtreme value) => CsvRow(
            TextCell(viewId), "moving_load", "", "static", "0", table,
            TextCell(entityId), TextCell(locationId), position, component, extreme,
            Format(value.Value), TextCell(value.CaseId));

    private static string MovingGlobalRows(string viewId, MemberForceExtremaComponents values)
    {
        (string Component, MemberForceScalarExtrema Value)[] components =
        [
            ("fx", values.Fx), ("fy", values.Fy), ("fz", values.Fz),
            ("mx", values.Mx), ("my", values.My), ("mz", values.Mz),
        ];
        return string.Concat(components.SelectMany(value => new[]
        {
            MovingGlobalRow(viewId, value.Component, "maximum", value.Value.Maximum),
            MovingGlobalRow(viewId, value.Component, "minimum", value.Value.Minimum),
            MovingGlobalRow(viewId, value.Component, "absolute_maximum", value.Value.AbsoluteMaximum),
        }));
    }

    private static string MovingGlobalRow(
        string viewId,
        string component,
        string extreme,
        MemberForceExtreme value) => CsvRow(
            TextCell(viewId), "moving_load", "", "static", "0", "member_force_extrema",
            TextCell(value.MemberId), TextCell(value.SegmentId), value.End.ToString(), component, extreme,
            Format(value.Value), TextCell(value.CaseId));

    private static string CsvRow(params string[] fields) => string.Join(',', fields) + "\r\n";

    private static string TextCell(string value)
    {
        bool neutralize = value.Length > 0 &&
            (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n' ||
                LeadingWhitespaceEndsInFormulaMarker(value));
        string safe = neutralize ? "'" + value : value;
        return safe.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + safe.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : safe;
    }

    private static bool LeadingWhitespaceEndsInFormulaMarker(string value)
    {
        int index = 0;
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            if (value[index] is '\t' or '\r' or '\n') return true;
            index++;
        }

        return index < value.Length && value[index] is '=' or '+' or '-' or '@';
    }

    private static AnalysisResultSet CreatePickupResultSet()
    {
        AnalysisResultSet source = ReadPositiveFixture("single-static.json");
        StaticAnalysisResult template = Assert.IsType<StaticAnalysisResult>(Assert.Single(source.Results));
        TopologyMember member = Assert.Single(source.Topology.Members);
        AnalysisTopology topology = new(
            source.Topology.Nodes,
            [new TopologyMember(
                "+10",
                member.NodeI,
                member.NodeJ,
                member.LocalFrame,
                [new MemberStation("S0", 0), new MemberStation("S1", 1)])],
            [],
            []);

        StaticAnalysisResult CreateResult(string caseId, ForceComponents iEnd, ForceComponents jEnd) => new(
            caseId,
            template.NodeDisplacements,
            template.SupportReactions,
            [new MemberSectionForces(
                "+10",
                [new MemberSegmentResult("S0-S1", "S0", "S1", 1, iEnd, jEnd)])],
            [],
            [],
            new WarningDiagnostics([]));

        AnalysisResultSet result = new(
            source.Kind,
            source.SchemaVersion,
            source.Units,
            source.CoordinateSystem,
            [
                new AnalysisCase("@A", "Formula source A", "A", AnalysisType.Static, ["1"]),
                new AnalysisCase("-B", "Formula source B", "B", AnalysisType.Static, ["1"]),
            ],
            topology,
            [
                CreateResult(
                    "@A",
                    new ForceComponents(10, -20, 30, -40, 50, -60),
                    new ForceComponents(1, 2, 3, 4, 5, 6)),
                CreateResult(
                    "-B",
                    new ForceComponents(-11, 21, -31, 41, -51, 61),
                    new ForceComponents(1, -2, -3, -4, -5, -6)),
            ]);
        AnalysisResultSetValidator.Validate(result);
        return result;
    }

    private static string PickupRow(
        string focus,
        string maximumSource,
        string minimumSource,
        string station,
        string end,
        double distance,
        ForceComponents maximum,
        ForceComponents minimum) =>
        $"'=PICK,{focus},'+10,{maximumSource},{minimumSource},{station},{end}," +
        $"{Format(distance)},1,{ForceValues(maximum)},{ForceValues(minimum)}\r\n";

    private static string Pickup2DRow(
        string focus,
        string member,
        string maximumSource,
        string minimumSource,
        string station,
        double distance,
        ForceComponents maximum,
        ForceComponents minimum) =>
        FixedText("=PICK", 5) +
        FixedText(focus, 5) +
        FixedText(member, 5) +
        FixedText(maximumSource, 5) +
        FixedText(minimumSource, 5) +
        FixedText(station, 5) +
        FixedNumber(distance, 10, "F3") +
        FixedNumber(maximum.Mz, 10, "F2") +
        FixedNumber(maximum.Fy, 10, "F2") +
        FixedNumber(maximum.Fx, 10, "F2") +
        FixedNumber(minimum.Mz, 10, "F2") +
        FixedNumber(minimum.Fy, 10, "F2") +
        FixedNumber(minimum.Fx, 10, "F2") +
        "\r\n";

    private static string ForceValues(ForceComponents value) => string.Join(
        ',',
        Format(value.Fx),
        Format(value.Fy),
        Format(value.Fz),
        Format(value.Mx),
        Format(value.My),
        Format(value.Mz));

    private static string FixedNumber(double value, int width, string format) =>
        FixedText(value.ToString(format, CultureInfo.InvariantCulture), width);

    private static string FixedText(string value, int width)
    {
        string singleLine = string.Concat(value.Select(character => char.IsControl(character) ? ' ' : character));
        return singleLine.Length >= width ? singleLine[^width..] : singleLine.PadLeft(width, ' ');
    }

    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static AnalysisResultSet ReadPositiveFixture(string fileName) => AnalysisResultSetJson.Deserialize(
        File.ReadAllBytes(Path.Combine(
            FindRepositoryRoot(),
            "FrameWeb",
            "tests",
            "data",
            "contracts",
            "positive",
            fileName)));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static StaticAnalysisResult CreateNodeOnlyStatic(string caseId, string nodeId) => new(
        caseId,
        [new NodeDisplacement(nodeId, Components())],
        [],
        [],
        [],
        [],
        new WarningDiagnostics([]));

    private static DisplacementComponents Components() =>
        new(1.25, -2.5, 3, 0.0001, -0.0002, 1.2345678901234567);
}
