using PdfSharpCore.Drawing;
using System.Collections.Generic;
using System.Linq;

namespace PDF_Manager.Printing
{
    /// <summary>
    /// 部材節点に関連する情報を保持する
    /// </summary>
    internal class XNode
    {
        /// <summary>
        /// 節点番号
        /// </summary>
        public string No { get; }
        /// <summary>
        /// 節点座標(isOlderVer2反映済み)
        /// </summary>
        public XPoint Pos { get; }

        /// <summary>
        /// 部材反対側の節点のコレクション
        /// </summary>
        public IEnumerable<XNode> Anothers { get; private set; } = Enumerable.Empty<XNode>();

        // 支点の描画情報は、現状では特に必要がないため保持しない
        //public XSupport Support { get; private set; } = null;

        /// <summary>
        /// 節点荷重の描画情報のコレクション
        /// </summary>
        public IEnumerable<IXNodalLoad> NodalLoads => nodalLoadList.AsEnumerable();

        /// <summary>
        /// 部材節点に関連する情報を生成する
        /// </summary>
        /// <param name="no">節点番号</param>
        /// <param name="inputNode">JSONファイルから読み込んだ節点情報</param>
        /// <param name="isOlderVer2">JSONファイルから読み込んだデータ中のY座標を補正するための係数</param>
        public XNode(string no, InputNode inputNode, int isOlderVer2)
        {
            No = no;
            var pos = inputNode.GetNodePos(no);
            Pos = new XPoint(pos.x, isOlderVer2 * pos.y);
        }

        public void SetAnothers(IReadOnlyCollection<XMember> members)
            => Anothers = members.Select(m => m.Ni.No == No ? m.Nj : m.Nj.No == No ? m.Ni : null).Where(n => n != null);

        //public void ClearSupport() => Support = null;
        //public void AddSupport(XSupport support) => Support = support;

        public void ClearNodalLoads() => nodalLoadList.Clear();

        public void AddNodalLoad(IXNodalLoad nodalLoad) => nodalLoadList.Add(nodalLoad);
        public void AddNodalLoads(IEnumerable<IXNodalLoad> nodalLoads) => nodalLoadList.AddRange(nodalLoads);

        private readonly List<IXNodalLoad> nodalLoadList = new List<IXNodalLoad>();
    }
}
