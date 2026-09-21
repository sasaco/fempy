using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Abstractions;
using PDF_Manager.Core.Documents;
using PDF_Manager.Resources;
using PDF_Manager.Shell;
using PDF_Manager.Shell.Editing;
using PDF_Manager.Shell.Printing;
using PDF_Manager.Shell.ScreenComposition.Core;
using PDF_Manager.Shell.ScreenComposition.Surfaces;
using System.Reflection;

namespace PDF_Manager.UiTests;

public sealed class Phase2QualityRemediationTests
{
    [Fact]
    public void WaitAndPrintOverlaysExposeOnlyActiveAngularControls()
    {
        StaTestRunner.Run(() =>
        {
            LocalizationService localization = new(UiLanguage.English);
            using OperationOverlayControl wait = new(localization, ScreenOverlayKind.Wait);
            wait.Size = new Size(1200, 800);
            wait.PerformLayout();
            Assert.True(wait.Spinner.Visible);
            Assert.Equal(new Size(90, 90), wait.Spinner.Size);
            Assert.Equal(wait.DialogPanel.ClientSize.Width / 2, wait.Spinner.Left + (wait.Spinner.Width / 2));
            Assert.Equal(wait.DialogPanel.ClientSize.Height / 2, wait.Spinner.Top + (wait.Spinner.Height / 2));
            Assert.Equal(Color.Transparent, wait.DialogPanel.BackColor);
            Assert.False(wait.HeaderPanel.Visible);
            Assert.False(wait.CloseButton.Visible);
            Assert.False(Assert.Single(wait.Controls.Find("OverlayTitle", true)).Visible);
            Assert.False(Assert.Single(wait.Controls.Find("OverlayContent", true)).Visible);
            Assert.False(Assert.Single(wait.Controls.Find("OperationOverlayMessage", true)).Visible);
            Assert.False(wait.PrimaryButton.Visible);
            Assert.False(wait.CancelOperationButton.Visible);
            Assert.False(Assert.Single(wait.Controls.Find("OperationOverlayActions", true)).Visible);
            Assert.Equal(
                [wait.Spinner],
                Descendants(wait).Where(control => control.Visible && control.Controls.Count == 0));
            using Bitmap waitBitmap = new(wait.Width, wait.Height);
            wait.DrawToBitmap(waitBitmap, wait.ClientRectangle);
            Rectangle spinnerBounds = BoundsRelativeTo(wait.Spinner, wait);
            int whitePixels = 0;
            for (int y = spinnerBounds.Top; y < spinnerBounds.Bottom; y++)
            {
                for (int x = spinnerBounds.Left; x < spinnerBounds.Right; x++)
                {
                    Color pixel = waitBitmap.GetPixel(x, y);
                    if (pixel.R >= 240 && pixel.G >= 240 && pixel.B >= 240)
                    {
                        whitePixels++;
                    }
                }
            }

            Assert.InRange(whitePixels, 500, 4_000);
            Color spinnerCenter = waitBitmap.GetPixel(
                spinnerBounds.Left + (spinnerBounds.Width / 2),
                spinnerBounds.Top + (spinnerBounds.Height / 2));
            Assert.False(spinnerCenter.R >= 240 && spinnerCenter.G >= 240 && spinnerCenter.B >= 240);

            using OperationOverlayControl confirm = new(localization, ScreenOverlayKind.Confirm);
            Assert.True(confirm.HeaderPanel.Visible);
            Assert.True(Assert.Single(confirm.Controls.Find("OverlayContent", true)).Visible);
            Assert.False(confirm.Spinner.Visible);
            Assert.True(confirm.PrimaryButton.Visible);
            Assert.True(confirm.CancelOperationButton.Visible);
            Assert.True(Assert.Single(confirm.Controls.Find("OperationOverlayActions", true)).Visible);

            using OperationOverlayControl alert = new(localization, ScreenOverlayKind.Alert);
            Assert.True(alert.HeaderPanel.Visible);
            Assert.True(Assert.Single(alert.Controls.Find("OverlayContent", true)).Visible);
            Assert.False(alert.Spinner.Visible);
            Assert.True(alert.PrimaryButton.Visible);
            Assert.False(alert.CancelOperationButton.Visible);

            using PrintOverlayControl print = new(localization);
            PrintContentSection[] visibleSections = Enum.GetValues<PrintContentSection>()
                .Where(section => section != PrintContentSection.ModelDiagram)
                .ToArray();
            print.SetSelection(new PrintPageSetupSelection(
                PrintPageSettings.Default,
                Enum.GetValues<PrintContentSection>()));
            string[] visibleLabels = print.SectionSelector.Items.Cast<object>()
                .Select(item => item.ToString()!)
                .ToArray();
            Assert.Equal(visibleSections.Length, visibleLabels.Length);
            Assert.DoesNotContain(localization["PrintSectionModelDiagram"], visibleLabels);
            Assert.Equal(Enum.GetValues<PrintContentSection>(), print.Selection.Sections);
        }, "Phase 2 Angular-active wait and print controls");
    }

    [Fact]
    public void RouteCardAndRegionReflowAfterHiddenResponsiveResizeAndNavigation()
    {
        StaTestRunner.Run(() =>
        {
            using MainForm form = CreateForm();
            form.SetDocument(Step5EditorMatrixTests.CreateFullDocument());
            form.RouteController.CloseOverlay();
            form.RouteController.Navigate(ScreenRouteId.InputElements);

            RoutePanelHostControl host = form.ScreenShell.RoutePanelHost;
            host.Dock = DockStyle.None;
            ResizeAndLayout(host, new Size(1200, 716));
            Assert.Equal(new Size(1200, 716), host.ClientSize);
            AssertRouteCardLayout(host, new Rectangle(770, 0, 430, 716));

            host.Visible = false;
            ResizeAndLayout(host, new Size(1024, 684));
            Assert.Equal(new Size(1024, 684), host.ClientSize);
            AssertRouteCardLayout(host, new Rectangle(594, 0, 430, 684));

            ResizeAndLayout(host, new Size(1440, 816));
            Assert.Equal(new Size(1440, 816), host.ClientSize);
            AssertRouteCardLayout(host, new Rectangle(1010, 0, 430, 816));

            host.Visible = true;
            host.PerformLayout();
            AssertRouteCardLayout(host, new Rectangle(1010, 0, 430, 816));

            host.Visible = false;
            ResizeAndLayout(host, new Size(1024, 684));
            form.RouteController.Navigate(ScreenRouteId.InputNodes);
            ResizeAndLayout(host, new Size(1440, 816));

            Assert.Equal(ScreenRouteId.InputNodes, host.ActiveRoute);
            Assert.Equal(new Size(1440, 816), host.ClientSize);
            AssertRouteCardLayout(host, new Rectangle(1010, 0, 430, 816));
        }, "Phase 2 responsive route-card relayout");
    }

    [Fact]
    public void NodesAndSupportsPaintOnlyTheProjectedAngularSheetAndGroupedHeader()
    {
        StaTestRunner.Run(() =>
        {
            LocalizationService localization = new(UiLanguage.Japanese);
            using MainForm form = new(new MainFormServices(localization: localization));
            form.RouteController.CloseOverlay();

            form.RouteController.Navigate(ScreenRouteId.InputNodes);
            InputRouteSurfaceControl nodes = Assert.IsType<InputRouteSurfaceControl>(
                form.ScreenShell.RoutePanelHost.ActiveSurface);
            LayoutRouteSurface(form.ScreenShell.RoutePanelHost, nodes);
            DataGridView nodeGrid = nodes.PrimaryGrid;
            Assert.Equal(48, nodeGrid.ColumnHeadersHeight);
            int nodeRight = ProjectedTableRight(nodeGrid);
            Assert.InRange(nodeRight, 225, 227);
            using (Bitmap bitmap = DrawControl(nodeGrid))
            {
                Assert.Equal(Color.FromArgb(84, 88, 94), bitmap.GetPixel(nodeRight + 12, 80));
                Assert.NotEqual(Color.FromArgb(84, 88, 94), bitmap.GetPixel(nodeRight - 12, 80));
                Assert.Equal(
                    Color.FromArgb(84, 88, 94),
                    bitmap.GetPixel(12, nodeGrid.ClientSize.Height - 12));
            }

            form.RouteController.Navigate(ScreenRouteId.InputSupports);
            InputRouteSurfaceControl supports = Assert.IsType<InputRouteSurfaceControl>(
                form.ScreenShell.RoutePanelHost.ActiveSurface);
            LayoutRouteSurface(form.ScreenShell.RoutePanelHost, supports);
            DataGridView supportGrid = supports.PrimaryGrid;
            Assert.Equal(100, supportGrid.ColumnHeadersHeight);
            Assert.Equal(
                "節点 | 変位拘束 | 回転拘束",
                supportGrid.AccessibleDescription);
            Assert.Equal("No", supportGrid.Columns["node"].HeaderText);
            Assert.Equal("X方向", supportGrid.Columns["tx"].HeaderText);
            Assert.Equal("Y方向", supportGrid.Columns["ty"].HeaderText);
            Assert.Equal("(kN・m/rad)", supportGrid.Columns["rz"].HeaderText);
            int supportRight = ProjectedTableRight(supportGrid);
            Assert.InRange(supportRight, 349, 352);
            Rectangle tx = ProjectedColumnBounds(supportGrid, "tx");
            using (Bitmap bitmap = DrawControl(supportGrid))
            {
                Color gridLine = Color.FromArgb(99, 103, 108);
                Color headerFill = Color.FromArgb(86, 88, 92);
                Color emptyFill = Color.FromArgb(84, 88, 94);
                Assert.Equal(headerFill, bitmap.GetPixel(tx.Right - 1, 24));
                Assert.Equal(gridLine, bitmap.GetPixel(tx.Right - 1, 75));
                Assert.Equal(emptyFill, bitmap.GetPixel(supportRight + 12, 120));
                Assert.NotEqual(emptyFill, bitmap.GetPixel(supportRight - 12, 120));
                Assert.Equal(emptyFill, bitmap.GetPixel(12, supportGrid.ClientSize.Height - 12));
            }
        }, "Phase 2 projected input sheet and grouped support header");
    }

    [Fact]
    public void EveryMultiTableInputRouteExposesEveryTypedTableThroughTheControlPanel()
    {
        StaTestRunner.Run(() =>
        {
            using MainForm form = CreateForm();
            form.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());
            form.RouteController.CloseOverlay();

            InputRouteSurfaceDefinition[] definitions = FrameWebSurfaceCatalog.InputRoutes
                .Where(definition => definition.Tables.Count > 1)
                .ToArray();
            Assert.NotEmpty(definitions);

            foreach ((InputRouteSurfaceDefinition definition, int routeIndex) in
                     definitions.Select((value, index) => (value, index)))
            {
                form.RouteController.Navigate(definition.Route);
                InputRouteSurfaceControl surface = Assert.IsType<InputRouteSurfaceControl>(
                    form.ScreenShell.RoutePanelHost.ActiveSurface);
                if (routeIndex == 0)
                {
                    RaiseClick(form.ScreenShell.OptionalHeader.ControlButton);
                }

                Assert.True(form.ScreenShell.OptionalHeader.IsControlPanelOpen);
                Assert.True(surface.IsTableControlPanelOpen);
                Assert.Equal(definition.Tables, surface.TableButtons.Keys.ToArray());
                foreach (InputTableKey table in definition.Tables)
                {
                    Button button = Assert.IsType<Button>(surface.TableButtons[table]);
                    Assert.NotEmpty(button.Text);
                    RaiseClick(button);
                    Assert.Equal(table, surface.ActiveTableKey);
                    Assert.Contains(button, surface.TableControls.Controls.Cast<Control>());
                    Assert.True(IsDescendantOf(surface.PrimaryGrid, surface));
                }
            }

            InputRouteSurfaceControl loads = NavigateInput(form, ScreenRouteId.InputLoads);
            Assert.Equal(
                [InputTableKey.NodalLoads, InputTableKey.MemberLoads, InputTableKey.PrescribedDisplacements],
                loads.TableButtons.Keys.ToArray());
        }, "Phase 2 multi-table route reachability");
    }

    [Fact]
    public void AllResultRoutesRetainTheSharedGridAndUseTheHeaderPager()
    {
        StaTestRunner.Run(() =>
        {
            using MainForm form = CreateForm();
            (ProjectDocument source, AnalysisResultSet rawResults) =
                Step6ViewportBehaviorTests.CreateSignedCaseFixture();
            ProjectDocument document = CreateResultPresentationDocument(source);
            form.SetDocument(document);
            form.DocumentHost.SetResult(NormalizeSignedResultSet(rawResults));
            form.RouteController.SetResultsEnabled(true);
            form.RouteController.CloseOverlay();

            Control? previousSurface = null;
            ResultRouteSurfaceDefinition[] routes = FrameWebSurfaceCatalog.ResultRoutes.ToArray();
            foreach ((ResultRouteSurfaceDefinition definition, int routeIndex) in
                     routes.Select((value, index) => (value, index)))
            {
                form.RouteController.Navigate(definition.Route);
                ResultRouteSurfaceControl surface = Assert.IsType<ResultRouteSurfaceControl>(
                    form.ScreenShell.RoutePanelHost.ActiveSurface);

                if (previousSurface is not null)
                {
                    Assert.True(previousSurface.IsDisposed);
                }

                Assert.Equal(definition.Category, surface.Category);
                Assert.Equal(definition.Context, surface.Substate);
                Assert.False(surface.ResultGrid.IsDisposed);
                Assert.True(IsDescendantOf(surface.ResultGrid, surface));
                Assert.True(
                    surface.ResultGrid.Rows.Count > 0,
                    $"{definition.Route} produced no rows; table={form.DocumentHost.ResultTableSelector.SelectedIndex}, " +
                    $"derived={form.DocumentHost.SelectedDerivedResultId ?? "base"}.");
                Assert.Empty(surface.Controls.Find("ResultRoutePreviousButton", true));
                Assert.Empty(surface.Controls.Find("ResultRouteNextButton", true));
                Assert.Empty(surface.Controls.Find("ResultRoutePageLabel", true));
                Assert.Equal(form.DocumentHost.ResultPageIndex, form.RouteController.State.Page.Index);
                Assert.Equal(form.DocumentHost.ResultPageCount, form.RouteController.State.Page.Count);

                if (routeIndex == 0)
                {
                    Assert.Equal(2, surface.ChildSelector.Items.Count);
                    surface.ChildSelector.SelectedIndex = 1;
                    Assert.Equal(1, form.DocumentHost.ResultPageIndex);
                    Assert.Equal(new ScreenPageState(1, 2), form.RouteController.State.Page);

                    RaiseClick(form.ScreenShell.OptionalHeader.PreviousPageButton);
                    Assert.Equal(0, form.DocumentHost.ResultPageIndex);
                    Assert.Equal(new ScreenPageState(0, 2), form.RouteController.State.Page);

                    RaiseClick(form.ScreenShell.OptionalHeader.NextPageButton);
                    Assert.Equal(1, form.DocumentHost.ResultPageIndex);
                    Assert.Equal(new ScreenPageState(1, 2), form.RouteController.State.Page);
                    Assert.Contains("2 / 2", form.ScreenShell.OptionalHeader.PageIndicator.AccessibleName);
                }

                previousSurface = surface;
            }

            Assert.Equal(routes.Length - 1, form.ScreenShell.RoutePanelHost.DisposedSurfaceCount);
        }, "Phase 2 all result route ownership and header paging");
    }

    [Theory]
    [InlineData("multiple-nonlinear.json", 3)]
    [InlineData("modal.json", 2)]
    public void HeaderPagerTracksNonlinearAndModalStateSelectors(string fixture, int expectedPageCount)
    {
        StaTestRunner.Run(() =>
        {
            using MainForm form = CreateForm();
            form.DocumentHost.SetResult(ReadFixture(fixture));
            form.RouteController.SetResultsEnabled(true);
            form.RouteController.CloseOverlay();
            form.RouteController.Navigate(ScreenRouteId.ResultBasicDisplacements);

            ResultRouteSurfaceControl surface = Assert.IsType<ResultRouteSurfaceControl>(
                form.ScreenShell.RoutePanelHost.ActiveSurface);
            surface.CaseSelector.SelectedIndex = surface.CaseSelector.Items.Count - 1;
            surface.StateSelector.SelectedIndex = surface.StateSelector.Items.Count - 1;

            Assert.Equal(expectedPageCount, form.DocumentHost.ResultPageCount);
            Assert.Equal(expectedPageCount - 1, form.DocumentHost.ResultPageIndex);
            Assert.Equal(
                new ScreenPageState(expectedPageCount - 1, expectedPageCount),
                form.RouteController.State.Page);

            RaiseClick(form.ScreenShell.OptionalHeader.PreviousPageButton);
            Assert.Equal(expectedPageCount - 2, form.DocumentHost.ResultPageIndex);
            Assert.Equal(expectedPageCount - 2, form.RouteController.State.Page.Index);
        }, $"Phase 2 {fixture} authoritative header pager");
    }

    private static MainForm CreateForm() => new(new MainFormServices(
        localization: new LocalizationService(UiLanguage.English)));

    private static void RaiseClick(Button button)
    {
        MethodInfo onClick = typeof(Button).GetMethod(
            "OnClick",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(Button).FullName, "OnClick");
        onClick.Invoke(button, [EventArgs.Empty]);
    }

    private static InputRouteSurfaceControl NavigateInput(MainForm form, ScreenRouteId route)
    {
        form.RouteController.Navigate(route);
        return Assert.IsType<InputRouteSurfaceControl>(form.ScreenShell.RoutePanelHost.ActiveSurface);
    }

    private static bool IsDescendantOf(Control child, Control ancestor)
    {
        for (Control? current = child; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static void ResizeAndLayout(RoutePanelHostControl host, Size clientSize)
    {
        host.ClientSize = clientSize;
        host.PerformLayout();
    }

    private static void AssertRouteCardLayout(RoutePanelHostControl host, Rectangle expectedBounds)
    {
        Control surface = Assert.IsAssignableFrom<Control>(host.ActiveSurface);
        IFloatingRouteSurface floating = Assert.IsAssignableFrom<IFloatingRouteSurface>(surface);
        Assert.Equal(host.ClientRectangle, surface.Bounds);
        Assert.Equal(expectedBounds, floating.FloatingCard.Bounds);
        Assert.NotNull(host.Region);
        using Bitmap bitmap = new(1, 1);
        using Graphics graphics = Graphics.FromImage(bitmap);
        Assert.Equal(expectedBounds, Rectangle.Round(host.Region.GetBounds(graphics)));
    }

    private static void LayoutRouteSurface(
        RoutePanelHostControl host,
        InputRouteSurfaceControl surface)
    {
        host.Dock = DockStyle.None;
        host.ClientSize = new Size(1200, 716);
        host.PerformLayout();
        surface.PerformLayout();
        surface.CardPanel.PerformLayout();
        surface.BodyPanel.PerformLayout();
        surface.PrimaryGrid.PerformLayout();
    }

    private static int ProjectedTableRight(DataGridView grid)
    {
        int right = grid.RowHeadersVisible ? grid.RowHeadersWidth : 0;
        foreach (DataGridViewColumn column in grid.Columns.Cast<DataGridViewColumn>()
                     .Where(column => column.Visible)
                     .OrderBy(column => column.DisplayIndex))
        {
            right += column.Width;
        }

        return right;
    }

    private static Rectangle ProjectedColumnBounds(DataGridView grid, string columnName)
    {
        int left = grid.RowHeadersVisible ? grid.RowHeadersWidth : 0;
        foreach (DataGridViewColumn column in grid.Columns.Cast<DataGridViewColumn>()
                     .Where(column => column.Visible)
                     .OrderBy(column => column.DisplayIndex))
        {
            if (string.Equals(column.Name, columnName, StringComparison.Ordinal))
            {
                return new Rectangle(left, 0, column.Width, grid.ColumnHeadersHeight);
            }

            left += column.Width;
        }

        throw new InvalidOperationException($"Projected column '{columnName}' is not visible.");
    }

    private static Bitmap DrawControl(Control control)
    {
        Bitmap bitmap = new(control.ClientSize.Width, control.ClientSize.Height);
        control.DrawToBitmap(bitmap, control.ClientRectangle);
        return bitmap;
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static Rectangle BoundsRelativeTo(Control control, Control ancestor)
    {
        Point location = control.Location;
        for (Control? parent = control.Parent; parent is not null && !ReferenceEquals(parent, ancestor); parent = parent.Parent)
        {
            location.Offset(parent.Location);
        }

        return new Rectangle(location, control.Size);
    }

    private static ProjectDocument CreateResultPresentationDocument(ProjectDocument source)
    {
        return new ProjectDocument(
            source.Version,
            source.Metadata,
            source.Nodes,
            source.Members,
            source.Supports,
            source.LoadCases,
            source.NodalLoads,
            [
                new DerivedResultDefinition(
                    "COMBINE",
                    "Combine",
                    DerivedResultKind.Combine,
                    [new DerivedResultTerm("C1", 1), new DerivedResultTerm("C2", 1)]),
                new DerivedResultDefinition(
                    "PICKUP",
                    "Pickup",
                    DerivedResultKind.Pickup,
                    [new DerivedResultTerm("C1", 1), new DerivedResultTerm("C2", 1)]),
            ],
            [new MovingLoadDefinition("MOVING", "Moving", ["C1", "C2"])],
            source.Selection,
            source.IsDirty,
            source.Sections,
            source.Dimension,
            source.ElementPropertySets,
            source.RigidZones,
            source.SupportSets,
            source.Panels,
            source.JointReleaseSets,
            source.NoticePoints,
            source.MemberSpringSets,
            source.PrescribedDisplacements,
            source.MemberLoads);
    }

    private static AnalysisResultSet NormalizeSignedResultSet(AnalysisResultSet value)
    {
        StaticAnalysisResult firstResult = Assert.IsType<StaticAnalysisResult>(value.Results[0]);
        double memberLength = firstResult.MemberSectionForces[0].Segments[0].Length;
        AnalysisTopology topology = new(
            value.Topology.Nodes,
            value.Topology.Members.Select(member => new TopologyMember(
                member.MemberId,
                member.NodeI,
                member.NodeJ,
                member.LocalFrame,
                [new MemberStation("S0", 0), new MemberStation("S1", memberLength)])),
            value.Topology.ShellElements,
            value.Topology.SolidElements);
        AnalysisResult[] results = value.Results.Select(result =>
        {
            StaticAnalysisResult source = Assert.IsType<StaticAnalysisResult>(result);
            return (AnalysisResult)new StaticAnalysisResult(
                source.CaseId,
                source.NodeDisplacements,
                source.SupportReactions,
                source.MemberSectionForces.Select(member => new MemberSectionForces(
                    member.MemberId,
                    member.Segments.Select(segment => new MemberSegmentResult(
                        "S0-S1",
                        "S0",
                        "S1",
                        segment.Length,
                        segment.IEnd,
                        segment.JEnd)))),
                source.ShellResults,
                source.SolidResults,
                source.Diagnostics);
        }).ToArray();
        AnalysisResultSet normalized = new(
            value.Kind,
            value.SchemaVersion,
            value.Units,
            value.CoordinateSystem,
            value.Cases,
            topology,
            results);
        AnalysisResultSetValidator.Validate(normalized);
        return normalized;
    }

    private static AnalysisResultSet ReadFixture(string fileName)
    {
        string path = Path.Combine(
            FindRepositoryRoot(),
            "FrameWeb",
            "tests",
            "data",
            "contracts",
            "positive",
            fileName);
        return AnalysisResultSetJson.Deserialize(File.ReadAllBytes(path));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
