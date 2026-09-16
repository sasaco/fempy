FROM node:18.16.0-alpine AS app

# 使用例
#  $ docker build --build-arg BUILD_SCRIPT="build-actions:staging" -t webdanforjs .
# 設定値
# AWSへイメージをpushする場合 "build-action"もしくは"build-actions:staging"
# Docker Desktop等を使ってlocalhostで動作確認する場合 "build-docker:local"
ARG BUILD_SCRIPT

# アプリケーションディレクトリ作成
WORKDIR /usr/src/app

# アプリケーションファイルをコピー
COPY . /usr/src/app

# ビルドエラー "FATAL ERROR: Reached heap limit Allocation failed - JavaScript heap out of memory" の対応
ENV NODE_OPTIONS="--max-old-space-size=8192"

RUN npm install -g @angular/cli@12.2.7 && npm install --legacy-peer-deps && npm install rxfire@6.0.3 --legacy-peer-deps

# Angular App のビルド
RUN npm run $BUILD_SCRIPT

# 実際のイメージ
FROM nginx:alpine

# compiled Angular application files を NGINX ドキュメントルートへコピー
COPY --from=app /usr/src/app/dist /var/www/html/

RUN ls /var/www/html/

# NGINX configuration file を Angular application実行のためにコピー
COPY ./nginx.conf /etc/nginx/conf.d/default.conf

# NGINXが使用するDockerポートをデフォルト80にセット
EXPOSE 80
