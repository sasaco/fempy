using PDF_Manager.Printing.Comon;
using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Pdf;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace PDF_Manager.Printing
{
    internal class PdfDocument
    {
        private PdfSharpCore.Pdf.PdfDocument document;
        public TrimMargins Margine;     // マージン
        public PdfPage currentPage;     // 現在のページ

        public XGraphics gfx;   // 描画するための

        // 文字に関する情報
        public XFont font_mic;  // 明朝フォント
        public XFont font_got;  // ゴシックフォント
        public XFont font_simsun;  // ゴシックフォント

        // 図形に関する情報
        public XPen xpen;

        private PageSize pageSize;
        private PageOrientation pageOrientation;        
        private string title;   // 全ページ共通 左上に印字するタイトル
                                // private string casename;    //  左上に印字するケース名

        /// <summary>
        /// 改ページ直後の座標
        /// </summary>
        public XPoint initialPos;
        /// <summary>
        /// 現在の座標
        /// </summary>
        public XPoint currentPos;

        public XPoint TitleArea;
        public XPoint CaseArea;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="pd">印刷設定が記録されている</param>
        public PdfDocument(PrintData pd, ref int indexPage)
        {
            //　新規ドキュメントの作成
            this.document = new PdfSharpCore.Pdf.PdfDocument();
            this.document.Info.Title = "FrameWebForJS";

            // フォントリゾルバーのグローバル登録
            var FontResolver = GlobalFontSettings.FontResolver.GetType();
            if (FontResolver.Name != "JapaneseFontResolver")
                GlobalFontSettings.FontResolver = new JapaneseFontResolver();

            // フォントの設定
            this.font_mic = new XFont("MS Mincho", printManager.FontSize, XFontStyle.Regular);
            this.font_got = new XFont("MS Gothic", diagramManager.FontSize, XFontStyle.Regular);
            this.font_simsun = new XFont("simsun", printManager.FontSize, XFontStyle.Regular);

            this.title = pd.title;

            // 新しいページを作成する
            this.NewPage(pd.pageSize, pd.pageOrientation, ref indexPage);

        }

        /// <summary>
        /// 用紙の印刷可能な範囲の大きさ
        /// </summary>
        public XSize currentPageSize
        {
            get
            {
                double height = this.currentPage.Height;
                height -= this.Margine.Top;
                height -= this.Margine.Bottom;

                double width = this.currentPage.Width;
                width -= this.Margine.Left;
                width -= this.Margine.Right;

                return new XSize(width, height);
            }
        }


        /// <summary>
        /// 改行する
        /// </summary>
        /// <param name="LF"></param>
        public void addCurrentY(double LF)
        {
            this.currentPos.Y += LF;
        }
        public void setCurrentX(double X)
        {
            this.currentPos.X = Margine.Left;
            this.currentPos.X += X;
        }

        /// <summary>
        /// 改ページ
        /// </summary>
        public void NewPage(string pageSize, string pageOrientation, ref int indexPage)
        {
            // 用紙の大きさ
            switch (pageSize)
            {
                case "A4":
                    this.pageSize = PageSize.A4;
                    break;
                case "A3":
                    this.pageSize = PageSize.A3;
                    break;
            }
            //ページの向き
            switch (pageOrientation)
            {
                case "Vertical":
                    this.pageOrientation = PageOrientation.Portrait;
                    break;
                case "Horizontal":
                    this.pageOrientation = PageOrientation.Landscape;
                    break;
            }

            this.setDefaultMargine();

            this.NewPage(ref indexPage);
        }

        /// <summary>
        /// マージンを設定する
        /// </summary>
        private void setDefaultMargine()
        {
            this.Margine = new TrimMargins();
            this.Margine.Top = 25;
            this.Margine.Left = 35;
            this.Margine.Right = 25;
            this.Margine.Bottom = 25;
        }

        /// <summary>
        /// 改ページ
        /// </summary>
        public void NewPage(ref int indexPage)
        {
            // 白紙をつくる（Ａ４縦または横）          
            this.currentPage = document.AddPage();
            this.currentPage.Size = this.pageSize;
            this.currentPage.Orientation = this.pageOrientation;
            var titleTime = new XFont("MS Mincho", 10, XFontStyle.Regular);
            // XGraphicsオブジェクトを取得
            this.gfx = XGraphics.FromPdfPage(this.currentPage);

            // 初期位置を設定する
            this.currentPos = new XPoint(Margine.Left, Margine.Top);
            this.currentPos.Y += printManager.titlePos.Y;
            this.currentPos.X += printManager.titlePos.X;

            // タイトルの印字
            if (this.title != null)
            {
                Text.PrtText(this, string.Format("TITLE : {0}", this.title));
            }
            this.currentPos.Y += printManager.FontHeight;
            this.currentPos.Y += printManager.LineSpacing2;
            this.gfx.DrawString($"Page   {indexPage.ToString()}", titleTime, XBrushes.Black, new XPoint(this.currentPage.Width - 70, 62), null);
            // 改ページ直後の座標の保存
            this.initialPos = this.currentPos;
            indexPage++;
        }

        /// <summary>
        /// PDF を Byte型に変換
        /// </summary>
        /// <returns></returns>
        public byte[] GetPDFBytes()
        {
            // Creates a new Memory stream
            MemoryStream stream = new MemoryStream();

            // Saves the document as stream
            this.document.Save(stream);
            this.document.Close();

            // Converts the PdfDocument object to byte form.
            byte[] docBytes = stream.ToArray();

            return docBytes;
        }

        /// <summary>
        /// PDF を保存する
        /// </summary>
        /// <param name="filename"></param>
        public void SavePDF(string filename = @"../../../TestData/Test.pdf")
        {
            // PDF保存（カレントディレクトリ）
            this.document.Save(filename);
        }

        /// <summary>
        /// 文字の大きさを取得する
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        internal XSize MeasureString(string text)
        {
            XSize result = this.gfx.MeasureString(text, this.font_mic);
            return result;
        }
    }
}


// 日本語フォントのためのフォントリゾルバー
internal class JapaneseFontResolver : IFontResolver
{
    public string DefaultFontName => throw new NotImplementedException();

    // MS 明朝
    public static readonly string MS_MINCHO_TTF = "PDF_Manager.fonts.MS Mincho.ttf";


    // MS ゴシック
    private static readonly string MS_GOTHIC_TTF = "PDF_Manager.fonts.MS Gothic.ttf";

    private static readonly string MS_SIMSUN_TTF = "PDF_Manager.fonts.simsun.ttf";


    public byte[] GetFont(string faceName)
    {
        switch (faceName)
        {
            case "MsMincho#Medium":
                return LoadFontData(MS_MINCHO_TTF);

            case "MsGothic#Medium":
                return LoadFontData(MS_GOTHIC_TTF);

            case "simsun#Medium":
                return LoadFontData(MS_SIMSUN_TTF);
        }
        return null;
    }

    public FontResolverInfo ResolveTypeface(
                string familyName, bool isBold, bool isItalic)
    {
        var fontName = familyName.ToLower();

        switch (fontName)
        {
            case "ms gothic":
                return new FontResolverInfo("MsGothic#Medium");
            case "simsun":
                return new FontResolverInfo("simsun#Medium");
        }

        // デフォルトのフォント
        return new FontResolverInfo("MsMincho#Medium");
    }

    // 埋め込みリソースからフォントファイルを読み込む
    private byte[] LoadFontData(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();

        using (Stream stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
                throw new ArgumentException("No resource with name " + resourceName);

            int count = (int)stream.Length;
            byte[] data = new byte[count];
            stream.Read(data, 0, count);
            return data;
        }
    }
}




