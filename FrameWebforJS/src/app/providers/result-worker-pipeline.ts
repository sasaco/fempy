export interface ResultWorkerReply {
  readonly error: string | null;
}

export class ResultWorkerPipelineError extends Error {
  constructor(readonly workerName: string, reason: string) {
    super(`${workerName}: ${reason}`);
    this.name = "ResultWorkerPipelineError";
  }
}

export function runResultWorker<Request, Reply extends ResultWorkerReply>(
  worker: Worker,
  workerName: string,
  request: Request
): Promise<Reply> {
  return new Promise<Reply>((resolve, reject) => {
    const cleanup = () => {
      worker.removeEventListener("message", onMessage);
      worker.removeEventListener("error", onError);
      worker.removeEventListener("messageerror", onMessageError);
    };
    const fail = (reason: string) => {
      cleanup();
      reject(new ResultWorkerPipelineError(workerName, reason));
    };
    const onMessage = (event: MessageEvent<Reply>) => {
      const reply = event.data;
      if (typeof reply !== "object" || reply === null || !("error" in reply)) {
        fail("worker returned an invalid reply");
        return;
      }
      if (reply.error !== null) {
        fail(reply.error);
        return;
      }
      cleanup();
      resolve(reply);
    };
    const onError = (event: ErrorEvent) => {
      event.preventDefault();
      fail(event.message || "worker execution failed");
    };
    const onMessageError = () => fail("worker reply could not be deserialized");

    worker.addEventListener("message", onMessage);
    worker.addEventListener("error", onError);
    worker.addEventListener("messageerror", onMessageError);
    try {
      worker.postMessage(request);
    } catch (error) {
      fail(error instanceof Error ? error.message : String(error));
    }
  });
}

export function workerErrorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
