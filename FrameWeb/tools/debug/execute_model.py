"""Run a model manually; generated JSON/VTK goes to an explicit output directory.

The optional --gui route requires Flet. Analysis follows the input selection.
"""

import argparse
import copy
import json
import os
from pathlib import Path

from app.error_handling import MyCritical, MyError
from fem.file_io import read_model, result_to_jsonable, write_vtk
from fem.model import FemModel
from tests.support.paths import DATA, ROOT


def output_result_json(data, result, source, output_dir):
    output_dir.mkdir(parents=True, exist_ok=True)
    output = output_dir / (source.stem + ".result.json")
    value = copy.deepcopy(data)
    value["result"] = result_to_jsonable(result)
    output.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    return output


def execute(page, output_dir):
    import flet as ft

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
    text1 = ft.Text(value="FEMPythonファイルの選択", size=20, weight=ft.FontWeight.BOLD)
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
        options=[ft.dropdown.Option("json", "JSON"), ft.dropdown.Option("vtk", "VTK")],
        value="json",
    )
    # エラーダイアログ
    errDlg = ft.AlertDialog(modal=True)
    # endregion

    # region アクション系のファンクション定義
    def on_file_selected(e: ft.FilePickerResultEvent):
        """ファイル選択ダイアログの処理"""
        if e.files:  # ファイルが選択された場合
            textField1.value = e.files[0].path  # テキストフィールド1にフルパスを出力

            button2.focus()  # フォーカスをボタン2に移動
        else:  # ファイルが選択されず閉じられた場合
            textField1.value = ""
        page.update()

    def close_Dialog(e):
        """エラーダイアログを閉じる"""
        errDlg.open = False
        page.update()

    def show_Dialog(msg: str):
        """エラーダイアログを表示する

        Args:
            msg (str): ダイアログに表示するメッセージ
        """
        errDlg.content = ft.Text(msg, overflow=ft.TextOverflow.VISIBLE, max_lines=10)
        errDlg.open = True
        page.update()

    def button1_click(e):
        """ファイル選択ボタンをクリックした時の処理"""
        filePicker.pick_files(
            allow_multiple=False, allowed_extensions=["json"], initial_directory=os.getcwd()
        )

    def button2_click(e):
        """計算実行ボタンをクリックした時の処理"""
        if not os.path.isfile(textField1.value):  # 入力ファイルの存在チェック
            show_Dialog("ファイルが存在しません")
            button1.focus()
            return
        # JSONモードのときのみ JSONロード
        jsonRaw = None
        try:
            with open(textField1.value, "r", encoding="utf-8") as f:
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
            result = fem_model.run()
            # 出力形式に応じて保存
            if formatDropdown.value == "json":
                output_result_json(jsonRaw, result, Path(textField1.value), output_dir)
            else:
                output_dir.mkdir(parents=True, exist_ok=True)
                vtk_path = str(output_dir / (Path(textField1.value).stem + ".vtk"))
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
    page.add(text1, button1, textField1, formatDropdown, button2)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("inputs", nargs="*", type=Path)
    parser.add_argument("--output-dir", type=Path, default=ROOT / "tmp/model-output")
    parser.add_argument("--format", choices=["json", "vtk"], default="json")
    parser.add_argument("--gui", action="store_true")
    args = parser.parse_args()
    output_dir = args.output_dir.resolve()
    if output_dir.is_relative_to(DATA.resolve()):
        parser.error("Generated output must be outside tests/data")
    if args.gui:
        import flet as ft

        ft.app(target=lambda page: execute(page, output_dir))
        return
    if not args.inputs:
        parser.error("Specify input paths or --gui")
    paths = [p for item in args.inputs for p in (sorted(item.glob("*.json")) if item.is_dir() else [item])]
    for path in paths:
        data = read_model(str(path))
        model = FemModel()
        model.read_json_model(data)
        result = model.run()
        directory = output_dir / path.parent.name if len(paths) > 1 else output_dir
        directory.mkdir(parents=True, exist_ok=True)
        if args.format == "vtk":
            output = directory / (path.stem + ".vtk")
            write_vtk(data, result, str(output))
        else:
            original = json.loads(path.read_text(encoding="utf8")) if path.suffix == ".json" else {}
            output = output_result_json(original, result, path, directory)
        print(output)


if __name__ == "__main__":
    main()
