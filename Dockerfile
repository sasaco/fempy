FROM python:3.11-slim

WORKDIR /app

COPY requirements.txt ./
COPY main.py ./
COPY src ./src

RUN pip install --no-cache-dir -r requirements.txt

CMD ["functions-framework", "--target=FrameWeb3", "--port=8080"] 