"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.getOrStartClient = getOrStartClient;
/**
 * Returns the client for a key and always waits for its idempotent startup.
 * This deliberately stores the client before starting it so concurrent callers
 * share the same client's start task (for example prewarm and an interactive
 * request arriving at the same time).
 */
async function getOrStartClient(clients, key, create, onCreate, onStartFailure) {
    let client = clients.get(key);
    if (!client) {
        client = create();
        clients.set(key, client);
        onCreate?.(client);
    }
    try {
        await client.start();
        return client;
    }
    catch (error) {
        if (clients.get(key) === client)
            clients.delete(key);
        onStartFailure?.(client, error);
        throw error;
    }
}
//# sourceMappingURL=hostLifecycle.js.map