using FrameWebforCS.components.input;
using FrameWebforCS.components.result;
using System;
using System.Collections.Generic;
using System.Text;

namespace FrameWebforCS
{
    internal class AppRoutingModule
    {
        // Lazy<T> を使ってスレッドセーフかつ遅延評価のシングルトンを実装
        private static readonly Lazy<AppRoutingModule> _instance =
            new Lazy<AppRoutingModule>(() => new AppRoutingModule());

        // 外部からはこのプロパティを通じてのみインスタンスにアクセスできる
        public static AppRoutingModule Instance => _instance.Value;


        // コンストラクタを private にして、外部からの new を禁止する
        private AppRoutingModule()
        {
            // 初期化処理があればここに書く
            CurrentType = null;
            CurrentTypeComponent = null;
        }

        // --------------------------------------------------
        // 保持したいデータやプロパティを以下に定義する
        // --------------------------------------------------
        public ToolStrip ContentsDailog { get; internal set; }
        private Type CurrentType { get; set; }
        private UserControl CurrentTypeComponent { get; set; }

        internal void contentsDailogShow(Type type)
        {
            if (CurrentType == type) return;

            ContentsDailog.Items.Clear();

            // 1. 独自のUserControlのインスタンスを作成
            UserControl CurrentUserControl = null;

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
            if (CurrentUserControl == null) return;

            // 2. ToolStripControlHostでラップする
            ToolStripControlHost hostControl = new ToolStripControlHost(CurrentUserControl);

            // 3. ToolStripに追加する
            ContentsDailog.Items.Add(hostControl);

            // 記憶
            CurrentType = type;
            CurrentTypeComponent = CurrentUserControl;

        }
    }
}
