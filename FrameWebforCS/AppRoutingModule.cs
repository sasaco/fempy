using Assimp.Unmanaged;
using FrameWebforCS.components.input;
using FrameWebforCS.components.result;
using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.Text;

namespace FrameWebforCS
{
    internal class AppRoutingModule
    {
        private InputDataService _input = InputDataService.Instance;
        // フォームを最前面に常時表示する設定
        Form? floatForm = null;

        // コンストラクタ
        public AppRoutingModule()
        {
        }


        internal void contentsDailogShow(Type type, string title)
        {
            if (_input.CurrentType == type) return;

            UserControl? CurrentUserControl = null;

            // 1. 独自のUserControlのインスタンスを作成
            if (type == typeof(InputElementsComponent))
            {
                CurrentUserControl = new InputElementsComponent();
            } 
            else if (type == typeof(InputNodesComponent))
            {
                CurrentUserControl = new InputNodesComponent();
            }
            else if (type == typeof(InputFixNodeComponent))
            {
                CurrentUserControl = new InputFixNodeComponent();
            }
            else if (type == typeof(InputMembersComponent))
            {
                CurrentUserControl = new InputMembersComponent();
            }
            else if (type == typeof(InputPanelComponent))
            {
                CurrentUserControl = new InputPanelComponent();
            }
            else if (type == typeof(InputJointComponent))
            {
                CurrentUserControl = new InputJointComponent();
            }
            else if (type == typeof(InputNoticePointsComponent))
            {
                CurrentUserControl = new InputNoticePointsComponent();
            }
            else if (type == typeof(InputFixMemberComponent))
            {
                CurrentUserControl = new InputFixMemberComponent();
            }
            else if (type == typeof(InputLoadNameComponent))
            {
                CurrentUserControl = new InputLoadNameComponent();
                // menu に load 追加
            }
            else if (type == typeof(InputDefineComponent))
            {
                CurrentUserControl = new InputDefineComponent();
                // menu に combine, pickup 追加
            }
            else if (type == typeof(ResultDisgComponent))
            {
                CurrentUserControl = new ResultDisgComponent();
                // menu に combine, pickup 追加
            }
            else if (type == typeof(ResultReacComponent))
            {
                CurrentUserControl = new ResultReacComponent();
                // menu に combine, pickup 追加
            }
            else if (type == typeof(ResultFsecComponent))
            {
                CurrentUserControl = new ResultFsecComponent();
                // menu に combine, pickup 追加
            }
          
            // 
            if (CurrentUserControl == null) 
                return;

            // 非モーダルで表示
            createFloatForm(CurrentUserControl, title);



            // 記憶
            _input.CurrentType = type;
        }

        private void createFloatForm(UserControl currentUserControl, string title)
        {
            if (floatForm == null)
                floatForm = new Form();
            if (floatForm.IsDisposed)
                floatForm = new Form();

            floatForm.Text = title;
            floatForm.Controls.Clear();
            floatForm.Controls.Add(currentUserControl);

            // サイズをコントロールの幅に合わせる
            Form? mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            if (mainForm != null) {
                floatForm.Owner = mainForm;
                floatForm.Height = (int)(mainForm.Height * 0.6);
            }
            floatForm.Width = currentUserControl.Width;
            currentUserControl.Dock = DockStyle.Fill;


            floatForm.Show();
        }
    }
}
