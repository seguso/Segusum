export interface StartableClient {
  start(): Promise<void>;
}

/**
 * Returns the client for a key and always waits for its idempotent startup.
 * This deliberately stores the client before starting it so concurrent callers
 * share the same client's start task (for example prewarm and an interactive
 * request arriving at the same time).
 */
export async function getOrStartClient<T extends StartableClient>(
  clients: Map<string, T>,
  key: string,
  create: () => T,
  onCreate?: (client: T) => void,
  onStartFailure?: (client: T, error: unknown) => void,
): Promise<T> {
  let client = clients.get(key);
  if (!client) {
    client = create();
    clients.set(key, client);
    onCreate?.(client);
  }

  try {
    await client.start();
    return client;
  } catch (error) {
    if (clients.get(key) === client) clients.delete(key);
    onStartFailure?.(client, error);
    throw error;
  }
}
