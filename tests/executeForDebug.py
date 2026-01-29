import os
import copy
import json
import flet as ft
from app.error_handling import MyError, MyCritical
from fem.model import FemModel
from fem.file_io import read_model, write_vtk
import sys
import glob
import argparse

# ここでsrcをパスに追加
src_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "src")
if src_path not in sys.path:
    sys.path.insert(0, src_path)

# FrameWeb3の単独実行用ファンクション
def execute(page: ft.Page):
    # region UIコンポーネントの作成
    # ウィンドウの設定
    page.title = "フレーム計算"
    page.window_width = 800
    page.window_height = 400
    page.window_maximizable = False  # 最小化ボタン無効
    page.window_maximizable = False  # 最大化ボタン無効
    page.window_resizable = False  # サイズ変更不可
    page.bgcolor = "#CFD8DC"  # Blue Grey 100 の hex コードに置き換え
    # テキスト
    text1 = ft.Text(value="FrameWebファイルの選択", size=20, weight=ft.FontWeight.BOLD)
    # ファイル選択ボタン
    button1 = ft.ElevatedButton("ファイルの選択", autofocus=True)
    # ファイルパス用テキストフィールド
    textField1 = ft.TextField("・・・.json", label="ファイルへのフルパス", disabled=True)
    # 計算実行ボタン
    button2 = ft.ElevatedButton("計算実行")
    # ファイル選択ダイアログ
    filePicker = ft.FilePicker()
    # 出力フォーマット選択
    formatDropdown = ft.Dropdown(
        label="出力形式",
        options=[
            ft.dropdown.Option("json", "JSON"),
            ft.dropdown.Option("vtk", "VTK")
        ],
        value="json"
    )
    # エラーダイアログ
    errDlg = ft.AlertDialog(modal=True)
    # endregion

    # region アクション系のファンクション定義
    def on_file_selected(e: ft.FilePickerResultEvent):
        """ファイル選択ダイアログの処理"""
        if e.files:  # ファイルが選択された場合
            textField1.value = e.files[0].path  # テキストフィールド1にフルパスを出力
            os.chdir(os.path.dirname(e.files[0].path))  # カレントディレクトリを変更
            button2.focus()  # フォーカスをボタン2に移動
        else:  # ファイルが選択されず閉じられた場合
            textField1.value = ""
        page.update()

    def close_Dialog(e):
        """エラーダイアログを閉じる"""
        errDlg.open = False
        page.update()

    def show_Dialog(msg: str):
        """ エラーダイアログを表示する

        Args:
            msg (str): ダイアログに表示するメッセージ
        """
        errDlg.content = ft.Text(msg, overflow=ft.TextOverflow.VISIBLE, max_lines=10)
        errDlg.open = True
        page.update()

    def button1_click(e):
        """ファイル選択ボタンをクリックした時の処理"""
        filePicker.pick_files(allow_multiple=False, allowed_extensions=["json"], initial_directory=os.getcwd())
    
    def button2_click(e):
        """計算実行ボタンをクリックした時の処理"""
        if not os.path.isfile(textField1.value):  # 入力ファイルの存在チェック
            show_Dialog("ファイルが存在しません")
            button1.focus()
            return
        # JSONモードのときのみ JSONロード
        jsonRaw = None
        try:
            with open(textField1.value, 'r', encoding="utf-8") as f:
                jsonRaw = json.load(f)
        except Exception as e:
            show_Dialog(f"JSONの読み込みに失敗しました: {e}")
            button1.focus()
            return
        # 計算実行
        try:
            # FEMモデル読み込み・解析
            model_data = read_model(textField1.value)
            fem_model = FemModel()
            fem_model.read_json_model(model_data)
            result = fem_model.run("static")
            # 出力形式に応じて保存
            if formatDropdown.value == "json":
                output_resultJson(jsonRaw, result, textField1.value)
            else:
                base, _ = os.path.splitext(textField1.value)
                vtk_path = base + ".vtk"
                write_vtk(model_data, result, vtk_path)
            show_Dialog("計算完了")
        except MyError as e:
            show_Dialog(e.output_msg())
        except MyCritical as e:
            show_Dialog(e.fixed_msg)
        except Exception as e:
            show_Dialog(str(e))
        finally:
            button1.focus()
    # endregion
    
    # region 登録
    page.overlay.append(filePicker)
    page.overlay.append(errDlg)
    page.dialog = errDlg
    button1.on_click = button1_click
    button2.on_click = button2_click
    filePicker.on_result = on_file_selected
    errDlg.actions = [ft.TextButton("閉じる", on_click=close_Dialog)]
    page.add(
        text1,
        button1,
        textField1,
        formatDropdown,
        button2
    )
    # endregion


def output_resultJson(jsonRaw: dict, result: dict, inputFile: str) -> None:
    """計算結果を追加したjsonデータを出力する

    Args:
        jsonRaw (dict): オリジナルの入力ファイルjsonの辞書データ
        result (dict): 計算結果の辞書データ
        inputFile (str): 入力データへのフルパス
    """
    outputJson: dict = copy.deepcopy(jsonRaw)
    outputJson["result"] = result
    # 入力ファイルと同じディレクトリに「{入力ファイル名}_out.json」で保存する
    base, _ = os.path.splitext(inputFile)
    outPath = base + "_out.json"
    with open(outPath, "w", encoding="utf-8") as f:
        json.dump(outputJson, f, indent=4, ensure_ascii=False)
    return None


# メインガードでバッチ処理を実装
if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="バッチ処理: JSONまたはVTK形式で解析結果を出力")
    parser.add_argument("path", nargs="?", help="JSONファイルまたはディレクトリへのパス")
    parser.add_argument("--format", "-f", choices=["json", "vtk"], default="json", help="出力フォーマット(json/vtk)")
    args = parser.parse_args()

    # バッチ処理モード: ディレクトリ指定
    if args.path and os.path.isdir(args.path):
        dir_path = args.path
        # サブフォルダも含めてすべてのJSONファイルを取得
        json_files = sorted(glob.glob(os.path.join(dir_path, "**", "*.json"), recursive=True))
        for json_file in json_files:
            try:
                # JSON読み込み
                jsonRaw = {}
                with open(json_file, "r", encoding="utf-8") as f:
                    jsonRaw = json.load(f)
                # モデル読み込み・解析
                model_data = read_model(json_file)
                fem_model = FemModel()
                fem_model.read_json_model(model_data)
                result = fem_model.run("static")

                # 出力
                if args.format == "json":
                    output_resultJson(jsonRaw, result, json_file)
                else:
                    base, _ = os.path.splitext(json_file)
                    out_path = base + ".vtk"
                    write_vtk(model_data, result, out_path)
                print(f"Processed {json_file} -> {args.format.upper()}")
            except Exception as e:
                print(f"Error processing {json_file}: {e}", file=sys.stderr)
    # ファイル指定モード
    elif args.path and os.path.isfile(args.path):
        json_file = args.path
        try:
            # JSONモードのときのみ JSON読み込み
            jsonRaw = None
            if args.format == "json":
                with open(json_file, "r", encoding="utf-8") as f:
                    jsonRaw = json.load(f)
            # モデル読み込み・解析
            model_data = read_model(json_file)
            fem_model = FemModel()
            fem_model.read_json_model(model_data)
            result = fem_model.run("static")

            # 出力処理
            if args.format == "json":
                output_resultJson(jsonRaw, result, json_file)
            else:
                base, _ = os.path.splitext(json_file)
                out_path = base + ".vtk"
                write_vtk(model_data, result, out_path)
            print(f"Processed {json_file} -> {args.format.upper()}")
        except Exception as e:
            print(f"Error processing {json_file}: {e}", file=sys.stderr)
    else:
        # GUIモード
        ft.app(target=execute)
