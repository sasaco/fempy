using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Shell;
using PDF_Manager.Resources;
using WeifenLuo.WinFormsUI.Docking;

namespace PDF_Manager.Shell.Contents;

public sealed class NavigationContent : ShellDockContent
{
    private readonly TreeView _tree = new()
    {
        BorderStyle = BorderStyle.None,
        Dock = DockStyle.Fill,
        HideSelection = false,
        Name = "NavigationTree",
    };

    private ProjectDocument? _document;

    public NavigationContent(DocumentKey contentKey, LocalizationService localization)
        : base(contentKey, localization)
    {
        DockAreas = DockAreas.DockLeft | DockAreas.DockRight | DockAreas.Float;
        ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.DockLeft;
        Controls.Add(_tree);
        ApplyLocalization();
    }

    public TreeView NavigationTree => _tree;

    public void SetDocument(ProjectDocument? document)
    {
        _document = document;
        RebuildTree();
    }

    public override void ApplyLocalization()
    {
        Text = Localization["PaneNavigation"];
        TabText = Text;
        RebuildTree();
    }

    private void RebuildTree()
    {
        _tree.BeginUpdate();
        try
        {
            _tree.Nodes.Clear();
            TreeNode model = _tree.Nodes.Add("model", Localization["NavigationModel"]);
            model.Nodes.Add("nodes", Format("NavigationNodes", _document?.Nodes.Count ?? 0));
            model.Nodes.Add("members", Format("NavigationMembers", _document?.Members.Count ?? 0));
            model.Nodes.Add("supports", Format("NavigationSupports", _document?.Supports.Count ?? 0));
            model.Nodes.Add("loads", Format("NavigationLoads", _document?.LoadCases.Count ?? 0));
            _tree.Nodes.Add("results", Localization["NavigationResults"]);
            model.Expand();
        }
        finally
        {
            _tree.EndUpdate();
        }
    }

    private string Format(string key, int value) =>
        string.Format(Localization.Culture, Localization[key], value);
}
