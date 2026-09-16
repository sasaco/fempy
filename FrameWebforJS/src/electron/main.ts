import { app, BrowserWindow, ipcMain, dialog, shell } from 'electron';
import { autoUpdater } from 'electron-updater';
import * as fs from 'fs';
import log from 'electron-log';
import isDev from 'electron-is-dev';
import path from 'path'
import { AuthProvider } from './login/authProvider';
import { IPC_MESSAGES } from './login/constants';
import { getGraphClient } from './login/graph';
import { msalConfig } from './login/config';
// 起動 --------------------------------------------------------------

let mainWindow: BrowserWindow;
let check = -1;
let locale = 'ja';
let authProvider : AuthProvider;
autoUpdater.autoDownload = false

// パッケージ化後の環境を考慮したパス解決
const getAssetsPath = () =>
  isDev
    ? path.join(__dirname, 'assets/i18n')
    : path.join(process.resourcesPath, 'app/assets/i18n');

export function getLangText(locale: string, fallback: string = 'ja'): any {
  const assetsPath = getAssetsPath();
  const readLangFile = (loc: string) => {
    const filePath = path.join(assetsPath, `${loc}.json`);
    const jsonString = fs.readFileSync(filePath, 'utf-8');
    return JSON.parse(jsonString);
  };

  try {
    return readLangFile(locale);
  } catch (error) {
    console.warn(`Failed to load locale '${locale}', falling back to '${fallback}'`, error);
    return readLangFile(fallback);
  }
}

async function createWindow() {
  check = -1;
  const successMessage = buildSuccessMessage(locale);
  authProvider = new AuthProvider(msalConfig, successMessage);
  mainWindow = new BrowserWindow({
    webPreferences: {
      nodeIntegration: true,
      contextIsolation: false,
    },
  });
  mainWindow.maximize();
  mainWindow.setMenuBarVisibility(false);
  // 外部サイト(http/https)はElectron内ではなくOSの既定ブラウザで開く。
  // blob: などアプリ内で開くべきURL(PDFプレビュー等)は従来どおりElectron内で許可する。
  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    if (url.startsWith('http://') || url.startsWith('https://')) {
      shell.openExternal(url);
      return { action: 'deny' };
    }
    return { action: 'allow' };
  });
  // mainWindow.webContents.openDevTools();
  mainWindow.on('close', function (e) {
    if (check == -1) {
      try {
        const langText = getLangText(locale)
        let choice = dialog.showMessageBoxSync(this,
          {
            type: 'question',
            buttons: ['Yes', 'No'],
            title: langText.window.closeTitle,
            message: langText.window.closeMessage,
          });
        if (choice == 1) {
          e.preventDefault();
        }
      } catch (error) {
        console.error('Failed to load language file:', error);
        e.preventDefault();
      }
    }
  });
  await mainWindow.loadFile('index.html');
}

app.whenReady().then(async () => {
  await createWindow();
  if (!isDev) {
    // 起動時に1回だけ
    await autoUpdater.checkForUpdates();
    log.info(`アップデートがあるか確認します。${app.name} ${app.getVersion()}`);
  }
});

autoUpdater.on('update-available', (info) => {
  log.info('update-available', info)
  autoUpdater.downloadUpdate();
});
autoUpdater.on('error', (err) => {
  log.info('Error in auto-updater:', err);
});
autoUpdater.on('download-progress', (progressObj) => {
  log.info('Download progress:', progressObj);
});
//when update downloaded, reboot to install
autoUpdater.on('update-downloaded', (info) => {
  log.info('Update-downloaded', info)
  try {
    const langText = getLangText(locale)
    let choice = dialog.showMessageBoxSync(mainWindow,
      {
        type: 'question',
        buttons: ["Ok", "Cancel"],
        message: langText.modal.updateMessage,
      });
    if (choice == 0) {
      let choice1 = dialog.showMessageBoxSync(mainWindow,
        {
          type: 'question',
          buttons: ['Yes', 'No'],
          title: langText.window.closeTitle,
          message: langText.window.closeMessage,
        });
      if (choice1 == 0) {
        check = 0;
        log.info("check install", check);
        autoUpdater.quitAndInstall();
      }
    }
  } catch (error) {
    console.error('Failed to load language file:', error);
  }

});
autoUpdater.checkForUpdates();
ipcMain.on("newWindow", async () => await createWindow());
// Angular -> Electron --------------------------------------------------
// ファイルを開く
ipcMain.on('open', (event: Electron.IpcMainEvent) => {
  // ファイルを選択
  const paths = dialog.showOpenDialogSync(mainWindow, {
    buttonLabel: 'open', // 確認ボタンのラベル
    filters: [{ name: 'json', extensions: ['json'] }],
    properties: [
      'openFile', // ファイルの選択を許可
      'createDirectory', // ディレクトリの作成を許可 (macOS)
    ],
  });

  // キャンセルで閉じた場合
  if (paths === undefined) {
    event.returnValue = { status: undefined };
    return;
  }

  // ファイルの内容を返却
  try {
    const path = paths[0];
    const buff = fs.readFileSync(path);

    // ファイルを読み込む
    event.returnValue = {
      status: true,
      path: path,
      text: buff.toString(),
    };
  } catch (error) {
    event.returnValue = { status: false, message: error.message };
  }
});

// 上書き保存
ipcMain.on(
  'overWrite',
  async (event: Electron.IpcMainEvent, path: string, data: string) => {
    fs.writeFile(path, data, async function (error) {
      if (error != null) {
        await dialog.showMessageBox({ message: 'error : ' + error });
      }
    });
    event.returnValue = path;
  }
);

// 名前を付けて保存
ipcMain.on(
  'saveFile',
  async (event: Electron.IpcMainEvent, filename: string, data: string, ext: string) => {
    // 場所とファイル名を選択
    const pathDownloads = app.getPath("downloads")
    filename = filename.split('\\').pop();
    const defaultPath = pathDownloads + "\\\\" + filename
    const path = dialog.showSaveDialogSync(mainWindow, {
      buttonLabel: 'save', // ボタンのラベル
      filters: [{ name: ext, extensions: [ext] }],
      defaultPath: defaultPath,
      properties: [
        'createDirectory', // ディレクトリの作成を許可 (macOS)
      ],
    });

    // キャンセルで閉じた場合
    if (path === undefined) {
      event.returnValue = '';
    }

    // ファイルの内容を返却
    try {
      fs.writeFileSync(path, data);
      event.returnValue = path;
    } catch (error) {
      await dialog.showMessageBox({ message: 'error : ' + error });
      event.returnValue = '';
    }
  }
);

// アラートを表示する
ipcMain.on(
  'alert',
  async (event: Electron.IpcMainEvent, message: string) => {
    await dialog.showMessageBox({ message });
    event.returnValue = '';
  }
);

ipcMain.on(
  'change-lang', (event, lang) => {
    locale = lang;
    const newSuccessMessage = buildSuccessMessage(locale)
    if (authProvider) {
      authProvider.setSuccessTemplate(newSuccessMessage)
    }
  })

// Event handlers
ipcMain.on(IPC_MESSAGES.LOGIN, async () => {
  const account = await authProvider.login();
  await mainWindow.loadFile(path.join(__dirname, "./index.html"));

  const tokenRequest = {
    account: account,
    scopes: []
  };

  const tokenResponse = await authProvider.getToken(tokenRequest);
  const userClaims = tokenResponse.idTokenClaims
  const listClaims = []
  if (userClaims) {
    Object.entries(userClaims).forEach((claim: [string, unknown], index: number) => {
      listClaims.push({ id: index, claim: claim[0], value: claim[1] });
    });
  }
  mainWindow.webContents.send(IPC_MESSAGES.GET_PROFILE, listClaims);
});

ipcMain.on(IPC_MESSAGES.LOGOUT, async () => {
  await authProvider.logout();
  await mainWindow.loadFile(path.join(__dirname, "./index.html"));
});

function buildSuccessMessage(locale: string): string {
  const langText = getLangText(locale);

  return `
    <!DOCTYPE html>
    <html lang="${locale}">
    <head>
      <meta charset="UTF-8">
      <title>My App</title>
    </head>
    <body>
      <h1>${langText.menu.loginSuccessTitle}</h1>
      <p>${langText.menu.loginSuccessDescription}</p>
    </body>
    </html>`;
}
