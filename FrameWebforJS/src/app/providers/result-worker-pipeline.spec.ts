import {
  ResultWorkerPipelineError,
  runResultWorker,
} from "./result-worker-pipeline";

class FakeWorker {
  public posted: unknown;
  private readonly listeners = new Map<string, Set<(event: any) => void>>();

  public addEventListener(type: string, listener: (event: any) => void): void {
    const entries = this.listeners.get(type) ?? new Set();
    entries.add(listener);
    this.listeners.set(type, entries);
  }

  public removeEventListener(type: string, listener: (event: any) => void): void {
    this.listeners.get(type)?.delete(listener);
  }

  public postMessage(value: unknown): void {
    this.posted = value;
  }

  public emit(type: string, event: any): void {
    this.listeners.get(type)?.forEach((listener) => listener(event));
  }
}

describe("result worker pipeline", () => {
  it("resolves only a typed successful worker reply", async () => {
    const fake = new FakeWorker();
    const completion = runResultWorker<
      { input: number },
      { rows: number[]; error: string | null }
    >(fake as unknown as Worker, "worker", { input: 1 });
    fake.emit("message", { data: { rows: [1], error: null } });

    await expectAsync(completion).toBeResolvedTo({ rows: [1], error: null });
    expect(fake.posted).toEqual({ input: 1 });
  });

  it("propagates a worker-reported failure", async () => {
    const fake = new FakeWorker();
    const completion = runResultWorker(fake as unknown as Worker, "worker", {});
    fake.emit("message", { data: { error: "bad result" } });

    await expectAsync(completion).toBeRejectedWithError(
      ResultWorkerPipelineError,
      "worker: bad result"
    );
  });

  it("propagates error and messageerror events", async () => {
    const executionWorker = new FakeWorker();
    const execution = runResultWorker(executionWorker as unknown as Worker, "execution", {});
    executionWorker.emit("error", { message: "boom", preventDefault: () => undefined });
    await expectAsync(execution).toBeRejectedWithError(
      ResultWorkerPipelineError,
      "execution: boom"
    );

    const messageWorker = new FakeWorker();
    const message = runResultWorker(messageWorker as unknown as Worker, "message", {});
    messageWorker.emit("messageerror", {});
    await expectAsync(message).toBeRejectedWithError(
      ResultWorkerPipelineError,
      "message: worker reply could not be deserialized"
    );
  });
});
