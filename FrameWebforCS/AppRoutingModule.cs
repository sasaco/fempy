using Assimp.Unmanaged;
using FrameWebforCS.components.input;
using FrameWebforCS.components.result;
using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.Text;
using THREE;
using static FrameWebforCS.components.menu.SidebarComponent;

namespace FrameWebforCS
{
    internal class AppRoutingModule
    {
        // Lazy<T> を使ってスレッドセーフかつ遅延評価のシングルトンを実装
        private static readonly Lazy<AppRoutingModule> _instance =
            new Lazy<AppRoutingModule>(() => new AppRoutingModule());

        // 外部からはこのプロパティを通じてのみインスタンスにアクセスできる
        public static AppRoutingModule Instance => _instance.Value;



        private InputDataService _input = InputDataService.Instance;
        // フォームを最前面に常時表示する設定
        Form? floatForm = null;

        public List<UserControl> myComponents = new List<UserControl>
        {
            { new InputElementsComponent() },
            { new  InputNodesComponent() },
            { new InputMembersComponent() },
            { new InputNoticePointsComponent() },
            { new InputPanelComponent() },
            { new InputFixNodeComponent() },
            { new InputFixMemberComponent()},
            { new InputJointComponent()},
            { new InputLoadComponent() },
            { new InputCombineComponent()},
            { new ResultDisgComponent() },
            { new ResultReacComponent() },
            { new ResultFsecComponent() }
        };

        // コンストラクタを private にして、外部からの new を禁止する
        private AppRoutingModule()
        {
        }


        internal void contentsDailogShow(Type _target, string title, int option = -1)
        {
            var target = myComponents.Find(x => x.GetType() == _target);
            if (target == null)
                return;

            if (floatForm == null) {
                floatForm = new Form();
            }
            else if (floatForm.IsDisposed) {
                floatForm = new Form();
                _input.CurrentComponent = null;
            }

            // setActiveSheet 関数がある場合は実行する
            var info = target.GetType().GetMethod("setActiveSheet");
            if (info != null)
                info.Invoke(target, new object[] { option });

            // 表示している画面と同じなら再表示しない
            if (_input.CurrentComponent != null) {
                if (_input.CurrentComponent.Equals(target))
                    return;
            }

            // 非モーダルで表示
            floatForm.Text = title;
            floatForm.Controls.Clear();
            floatForm.Controls.Add(target);

            // サイズをコントロールの幅に合わせる
            Form? mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            if (mainForm != null) {
                floatForm.Owner = mainForm;
                floatForm.Height = (int)(mainForm.Height * 0.6);
            }
            floatForm.Width = target.Width;
            target.Dock = DockStyle.Fill;

            floatForm.Show();

            // 記憶
            _input.CurrentComponent = target;

        }
    }
}
