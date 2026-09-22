using PDF_Manager.Printing;

namespace PDF_Manager.Tests;

public sealed class Step8PrintPlanningAcceptanceTests
{
    [Fact]
    public void A4PortraitTablePagination_IsDeterministicAndRepeatsHeaders()
    {
        PrintTable table = CreateTable("Node displacements", rowCount: 100);
        PrintJob job = new(
            "Deterministic pagination",
            PrintPageSettings.CreateA4(PrintPageOrientation.Portrait),
            PrintTextLanguage.English,
            [new PrintTableSection(table)]);

        PrintDocumentPlan first = Plan(job);
        PrintDocumentPlan second = Plan(job);

        Assert.Equal(3, first.PageCount);
        Assert.Equal([1, 2, 3], first.Pages.Select(page => page.PageNumber));
        Assert.Equal([44, 45, 11], first.Pages.Select(page => Assert.Single(page.Items).TableRowCount));
        Assert.Equal([0, 44, 89], first.Pages.Select(page => Assert.Single(page.Items).TableRowStart));
        Assert.Equal([false, true, true], first.Pages.Select(page => Assert.Single(page.Items).RepeatsTableHeader));
        Assert.Equal(
            first.Pages.SelectMany(page => page.Items),
            second.Pages.SelectMany(page => page.Items));
        Assert.Equal(PrintUnits.MillimetresToPoints(210), first.PageWidthPoints, precision: 10);
        Assert.Equal(PrintUnits.MillimetresToPoints(297), first.PageHeightPoints, precision: 10);
    }

    [Fact]
    public void A3LandscapePlan_PreservesMarginsScaleSectionOrderAndDiagramPlacement()
    {
        PrintPageSettings settings = new(
            PrintPaperSize.A3,
            PrintPageOrientation.Landscape,
            new PrintMargins(15, 12, 17, 14),
            scale: 1.5,
            showPageNumbers: true);
        PrintJob job = new(
            "橋梁モデル",
            settings,
            PrintTextLanguage.Japanese,
            [
                new PrintTextSection("入力データ", "節点・部材・荷重"),
                new PrintDiagramSection(new PrintDiagram(
                    "荷重図",
                    CreateCapture(32, 16),
                    PrintDiagramKind.Load,
                    preferredHeightMillimetres: 80)),
                new PrintResultSection(new PrintResult(
                    "LC-MOVING",
                    PrintResultQuantity.SectionForce,
                    "PICKUP max: LC-3 / min: LC-7",
                    CreateTable("断面力", rowCount: 3))),
            ]);

        PrintDocumentPlan plan = Plan(job);

        Assert.Equal(1, plan.PageCount);
        Assert.Equal(
            [PrintSectionKind.Text, PrintSectionKind.Diagram, PrintSectionKind.Result],
            plan.Pages[0].Items.Select(item => item.Kind));
        PrintPageItemPlan diagram = plan.Pages[0].Items[1];
        Assert.Equal(1, diagram.SectionIndex);
        Assert.True(diagram.Bounds.XPoints >= PrintUnits.MillimetresToPoints(15));
        Assert.True(diagram.Bounds.WidthPoints <= PrintUnits.MillimetresToPoints(388));
        Assert.True(diagram.Bounds.HeightPoints > PrintUnits.MillimetresToPoints(80));
        PrintPageItemPlan result = plan.Pages[0].Items[2];
        Assert.Equal(PrintSectionKind.Result, result.Kind);
        Assert.Equal(2, result.SectionIndex);
        Assert.Equal(3, result.TableRowCount);
    }

    [Fact]
    public void SectionPerPage_UsesExactPageBudgetAndRejectsPlusOne()
    {
        PrintJob job = new(
            "Page budget",
            PrintPageSettings.CreateA4(),
            PrintTextLanguage.English,
            [new PrintTextSection("One", "Body"), new PrintTextSection("Two", "Body")],
            PrintLayoutMode.SectionPerPage);
        PrintEngineLimitProfile exact = Limits(maximumPages: 2);

        PrintDocumentPlan plan = PrintLayoutPlanner.Plan(job, new PrintWorkBudget(exact), CancellationToken.None);
        Assert.Equal(2, plan.PageCount);

        PrintLimitExceededException exception = Assert.Throws<PrintLimitExceededException>(() =>
            PrintLayoutPlanner.Plan(job, new PrintWorkBudget(exact with { MaximumPages = 1 }), CancellationToken.None));
        Assert.Equal("pages", exception.ResourceName);
        Assert.Equal(1, exception.Limit);
    }

    [Fact]
    public void SharedBudget_AcceptsEveryExactLimitAndRejectsEveryPlusOne()
    {
        PrintEngineLimitProfile limits = Limits(
            maximumPages: 2,
            maximumSections: 2,
            maximumTables: 2,
            maximumRows: 2,
            maximumCells: 4,
            maximumTextCharacters: 5,
            maximumImages: 2,
            maximumDecodedImageBytes: 6,
            maximumEncodedPdfBytes: 7,
            maximumLayoutWork: 8);
        PrintWorkBudget exact = new(limits);

        exact.AddPage();
        exact.AddPage();
        exact.AddSection();
        exact.AddSection();
        exact.AddTable();
        exact.AddTable();
        exact.AddRows(2);
        exact.AddCells(4);
        exact.AddText("12345");
        exact.AddImage(CreateCapture(1, 1));
        exact.AddImage(CreateCapture(1, 1));
        exact.AddEncodedPdfBytes(7);
        exact.AddLayoutWork(8);

        AssertBudgetPlusOne(
            limits,
            static budget =>
            {
                budget.AddPage();
                budget.AddPage();
            },
            static budget => budget.AddPage(),
            "pages");
        AssertBudgetPlusOne(
            limits,
            static budget =>
            {
                budget.AddSection();
                budget.AddSection();
            },
            static budget => budget.AddSection(),
            "sections");
        AssertBudgetPlusOne(
            limits,
            static budget =>
            {
                budget.AddTable();
                budget.AddTable();
            },
            static budget => budget.AddTable(),
            "tables");
        AssertBudgetPlusOne(limits, static budget => budget.AddRows(2), static budget => budget.AddRows(1), "rows");
        AssertBudgetPlusOne(limits, static budget => budget.AddCells(4), static budget => budget.AddCells(1), "cells");
        AssertBudgetPlusOne(limits, static budget => budget.AddText("12345"), static budget => budget.AddText("x"), "text characters");
        AssertBudgetPlusOne(
            limits,
            budget =>
            {
                budget.AddImage(CreateCapture(1, 1));
                budget.AddImage(CreateCapture(1, 1));
            },
            budget => budget.AddImage(CreateCapture(1, 1)),
            "images");
        AssertBudgetPlusOne(
            limits with { MaximumImages = 3, MaximumDecodedImageBytes = 6 },
            budget =>
            {
                budget.AddImage(CreateCapture(1, 1));
                budget.AddImage(CreateCapture(1, 1));
            },
            budget => budget.AddImage(CreateCapture(1, 1)),
            "decoded image bytes");
        AssertBudgetPlusOne(
            limits,
            static budget => budget.AddEncodedPdfBytes(7),
            static budget => budget.AddEncodedPdfBytes(1),
            "encoded PDF bytes");
        AssertBudgetPlusOne(
            limits,
            static budget => budget.AddLayoutWork(8),
            static budget => budget.AddLayoutWork(),
            "layout work");
    }

    [Fact]
    public void DeclaredImageDimensionLimit_IsReachableAndPlusOneFailsBeforeLengthCalculation()
    {
        Assert.Equal(
            ViewportCapture.MaximumBytes,
            ViewportCapture.GetRequiredByteLength(
                PrintEngineLimits.MaximumImageDimension,
                PrintEngineLimits.MaximumImageDimension));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ViewportCapture.GetRequiredByteLength(PrintEngineLimits.MaximumImageDimension + 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ViewportCapture.GetRequiredByteLength(1, PrintEngineLimits.MaximumImageDimension + 1));
    }

    [Fact]
    public void Plan_PreCancelledTokenStopsBeforeLayoutOrImageEncoding()
    {
        PrintJob job = new(
            "Cancellation",
            PrintPageSettings.CreateA4(),
            PrintTextLanguage.English,
            [new PrintDiagramSection(new PrintDiagram("Model", CreateCapture(1, 1)))]);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            PrintLayoutPlanner.Plan(job, new PrintWorkBudget(PrintEngineLimitProfile.Default), cancellation.Token));
    }

    private static PrintDocumentPlan Plan(PrintJob job) =>
        PrintLayoutPlanner.Plan(job, new PrintWorkBudget(PrintEngineLimitProfile.Default), CancellationToken.None);

    private static PrintTable CreateTable(string caption, int rowCount)
    {
        PrintTableColumn[] columns =
        [
            new("ID"),
            new("X", alignment: PrintCellAlignment.Right),
            new("Y", alignment: PrintCellAlignment.Right),
        ];
        PrintTableRow[] rows = Enumerable.Range(1, rowCount)
            .Select(index => new PrintTableRow([index.ToString(), $"{index}.1", $"-{index}.2"]))
            .ToArray();
        return new PrintTable(caption, columns, rows, repeatHeader: true);
    }

    private static ViewportCapture CreateCapture(int width, int height) =>
        new(width, height, new byte[ViewportCapture.GetRequiredByteLength(width, height)]);

    private static PrintEngineLimitProfile Limits(
        int maximumPages = 16,
        int maximumSections = 16,
        int maximumTables = 16,
        int maximumRows = 256,
        int maximumCells = 1_024,
        int maximumTextCharacters = 16_384,
        int maximumImages = 16,
        long maximumDecodedImageBytes = 1_024 * 1_024,
        long maximumEncodedPdfBytes = 1_024 * 1_024,
        long maximumLayoutWork = 1_024 * 1_024) => new(
        maximumPages,
        maximumSections,
        maximumTables,
        maximumRows,
        maximumCells,
        maximumTextCharacters,
        maximumImages,
        maximumDecodedImageBytes,
        maximumEncodedPdfBytes,
        maximumLayoutWork);

    private static void AssertBudgetPlusOne(
        PrintEngineLimitProfile limits,
        Action<PrintWorkBudget> fill,
        Action<PrintWorkBudget> exceed,
        string expectedResourceName)
    {
        PrintWorkBudget budget = new(limits);
        fill(budget);
        PrintLimitExceededException exception = Assert.Throws<PrintLimitExceededException>(() => exceed(budget));
        Assert.Equal(expectedResourceName, exception.ResourceName);
    }
}
