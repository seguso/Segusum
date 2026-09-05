"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.activate = activate;
exports.deactivate = deactivate;
const vscode = __importStar(require("vscode"));
const child_process_1 = require("child_process");
const path = __importStar(require("path"));
const fs = __importStar(require("fs"));
const pendingRequests_1 = require("./pendingRequests");
const invalidation_1 = require("./invalidation");
const BUILD_ID = 'extension build = invalidation-lifecycle-2026-09-05';
const INTERACTIVE_RPC_TIMEOUT_MS = 15_000;
const interactiveMethods = new Set(['definition', 'completion', 'references', 'rename']);
let output;
let status;
function log(message) { output?.appendLine(`[${new Date().toISOString()}] ${message}`); }
class HostClient {
    projectPath;
    workspacePath;
    onDead;
    child;
    next = 1;
    pending = new pendingRequests_1.PendingRequestRegistry();
    dead = false;
    invalidation;
    get pendingCount() { return this.pending.size; }
    worlds = [];
    startTask;
    get isReady() { return !this.dead && !!this.child && this.worlds.length > 0; }
    get isDead() { return this.dead; }
    constructor(projectPath, workspacePath, onDead) {
        this.projectPath = projectPath;
        this.workspacePath = workspacePath;
        this.onDead = onDead;
        this.invalidation = new invalidation_1.InvalidationScheduler({
            send: () => this.request('invalidate', {}),
            log: message => log(`${message} project=${this.projectPath} pending=${this.pendingCount}`),
        });
    }
    async start() {
        if (!this.startTask)
            this.startTask = this.startCore();
        return this.startTask;
    }
    async startCore() {
        const started = Date.now();
        const configured = vscode.workspace.getConfiguration('segusum').get('toolingHostPath');
        const dll = configured || path.join(this.workspacePath, 'Segusum.Tooling.Host', 'bin', 'Debug', 'net8.0', 'Segusum.Tooling.Host.dll');
        log(`${BUILD_ID}`);
        log(`Starting host=${dll} project=${this.projectPath}`);
        if (!fs.existsSync(dll))
            throw new Error(`Tooling host not found: ${dll}`);
        this.dead = false;
        this.child = (0, child_process_1.spawn)('dotnet', [dll], { cwd: this.workspacePath, stdio: ['pipe', 'pipe', 'pipe'] });
        this.child.on('error', error => this.markDead(error));
        this.child.on('exit', (code, signal) => this.markDead(new Error(`Tooling host exited (code=${code ?? 'null'}, signal=${signal ?? 'null'})`)));
        this.child.on('close', (code, signal) => this.markDead(new Error(`Tooling host closed (code=${code ?? 'null'}, signal=${signal ?? 'null'})`)));
        log(`host process spawned project=${this.projectPath} elapsed=${Date.now() - started}ms`);
        let buffer = '';
        this.child.stdout.on('data', data => { buffer += data.toString(); let end; while ((end = buffer.indexOf('\n')) >= 0) {
            const line = buffer.slice(0, end);
            buffer = buffer.slice(end + 1);
            if (!line.trim())
                continue;
            try {
                const response = JSON.parse(line);
                const settled = response.error ? this.pending.reject(response.id, new Error(response.error.message)) : this.pending.resolve(response.id, response.result);
                if (!settled)
                    log(`RPC response ignored #${response.id} (late) pending=${this.pendingCount}`);
            }
            catch (e) {
                log(`Invalid host response: ${e}`);
            }
        } });
        this.child.stderr.on('data', data => log(`host stderr: ${data.toString().trim()}`));
        const initialized = await this.request('initialize', { projectPath: this.projectPath });
        if (this.dead)
            throw new Error('Tooling host stopped during initialization.');
        this.worlds = initialized?.worlds ?? [];
        log(`Host initialized project=${initialized?.projectPath ?? this.projectPath} worlds=${this.worlds.map((x) => x.id).join(',')} elapsed=${Date.now() - started}ms`);
    }
    request(method, params, token) {
        const id = this.next++;
        log(`RPC start #${id} ${method} project=${this.projectPath} pending=${this.pendingCount + 1}`);
        return new Promise((resolve, reject) => {
            if (this.dead || !this.child) {
                reject(new Error('Tooling host is not running.'));
                return;
            }
            if (token?.isCancellationRequested) {
                log(`RPC cancelled locally #${id} before send pending=${this.pendingCount}`);
                reject(new Error('Request cancelled'));
                return;
            }
            let subscription;
            let timer;
            this.pending.add(id, {
                resolve: value => { log(`RPC end #${id} ${method} pending=${this.pendingCount}`); resolve(value); },
                reject: error => { log(`RPC error #${id} ${method}: ${error} pending=${this.pendingCount}`); reject(error); },
                dispose: () => { subscription?.dispose(); if (timer)
                    clearTimeout(timer); },
            });
            subscription = token?.onCancellationRequested(() => this.cancel(id));
            if (!this.pending.has(id))
                return;
            if (token?.isCancellationRequested) {
                this.cancel(id);
                return;
            }
            if (interactiveMethods.has(method)) {
                timer = setTimeout(() => {
                    if (this.pending.reject(id, new Error(`RPC '${method}' timed out after ${INTERACTIVE_RPC_TIMEOUT_MS}ms`))) {
                        log(`RPC timeout #${id} ${method}; host marked unhealthy pending=${this.pendingCount}`);
                        this.markDead(new Error(`RPC '${method}' timed out`));
                    }
                }, INTERACTIVE_RPC_TIMEOUT_MS);
            }
            try {
                this.child.stdin.write(JSON.stringify({ id, method, params }) + '\n');
            }
            catch (error) {
                this.pending.reject(id, error);
                this.markDead(error instanceof Error ? error : new Error(String(error)));
            }
        });
    }
    cancel(id) { const cancelled = this.pending.reject(id, new Error('Request cancelled')); if (cancelled)
        log(`RPC cancelled locally #${id} pending=${this.pendingCount}`); try {
        if (!this.dead)
            this.child?.stdin.write(JSON.stringify({ id: this.next++, method: 'cancel', params: { requestId: id } }) + '\n');
    }
    catch (e) {
        log(`RPC cancel send failed #${id}: ${e}`);
    } }
    invalidate() { this.invalidation.request(); }
    markDead(error) {
        if (this.dead)
            return;
        this.dead = true;
        this.worlds = [];
        this.invalidation.dispose();
        this.pending.clear(error);
        log(`host exited/stopped project=${this.projectPath}; pending rejected; pending=${this.pendingCount}`);
        this.onDead(this, error);
    }
    dispose() { this.invalidation.dispose(); this.markDead(new Error('Host stopped')); this.child?.kill(); this.child = undefined; }
}
let requestCts;
const clients = new Map();
const projectByFolder = new Map();
const completionCts = new Map();
const refs = new Map();
class ReferenceNode extends vscode.TreeItem {
    value;
    constructor(value) {
        const file = value.file ?? value.path;
        const folder = file ? vscode.workspace.getWorkspaceFolder(vscode.Uri.file(file)) : undefined;
        const relative = file && folder ? path.relative(folder.uri.fsPath, file) : file;
        super(value.path ? `${value.line}: ${value.preview ?? value.displayName}` : path.basename(file ?? ''), value.path ? vscode.TreeItemCollapsibleState.None : vscode.TreeItemCollapsibleState.Expanded);
        this.value = value;
        this.description = value.path ? value.language : relative;
        this.tooltip = relative;
        if (value.path)
            this.command = { command: 'segusum.openReference', title: 'Open reference', arguments: [value] };
    }
}
class ReferenceProvider {
    change = new vscode.EventEmitter();
    onDidChangeTreeData = this.change.event;
    getTreeItem(node) { return node; }
    getChildren(node) { if (node)
        return (refs.get(node.value.file) ?? []).map(x => new ReferenceNode(x)); return [...refs.keys()].sort((a, b) => a.localeCompare(b)).map(file => new ReferenceNode({ file })); }
    refresh() { this.change.fire(); }
}
const referenceProvider = new ReferenceProvider();
async function discoverProject(folder) {
    const files = await vscode.workspace.findFiles(new vscode.RelativePattern(folder, '**/*.csproj'), '**/{bin,obj,node_modules}/**');
    const segs = await vscode.workspace.findFiles(new vscode.RelativePattern(folder, '**/*.seg'), '**/{bin,obj,node_modules}/**');
    return files.filter(file => segs.some(seg => seg.fsPath.startsWith(path.dirname(file.fsPath) + path.sep))).sort((a, b) => a.fsPath.localeCompare(b.fsPath))[0]?.fsPath;
}
function folderFor(document) { const folder = vscode.workspace.getWorkspaceFolder(document.uri); if (!folder)
    throw new Error(`No workspace folder owns ${document.uri.fsPath}`); return folder; }
function worldId(document) { const match = document.getText().match(/^\s*world\s+([A-Za-z_][A-Za-z0-9_-]*)/m); return match?.[1] ?? '(unknown)'; }
async function clientFor(document) {
    const started = Date.now();
    const folder = folderFor(document);
    const folderKey = folder.uri.toString();
    let projectPath = projectByFolder.get(folderKey);
    if (!projectPath) {
        projectPath = await discoverProject(folder);
        if (projectPath)
            projectByFolder.set(folderKey, projectPath);
    }
    if (!projectPath)
        throw new Error(`No consumer .csproj containing .seg files found under ${folder.uri.fsPath}`);
    const discoveryMs = Date.now() - started;
    const key = path.normalize(projectPath).toLowerCase();
    let client = clients.get(key);
    if (client?.isDead) {
        clients.delete(key);
        client = undefined;
    }
    if (!client) {
        client = new HostClient(projectPath, folder.uri.fsPath, (deadClient, error) => { if (clients.get(key) === deadClient)
            clients.delete(key); status.text = 'Segusum: Error'; status.tooltip = `Project: ${projectPath}\n${error.message}`; status.show(); });
        clients.set(key, client);
        status.text = 'Segusum: Loading';
        status.tooltip = `Project: ${projectPath}\nLoading semantic workspace...`;
        status.show();
        try {
            await client.start();
        }
        catch (e) {
            clients.delete(key);
            client.dispose();
            status.text = 'Segusum: Error';
            status.tooltip = `Project: ${projectPath}\n${e}`;
            status.show();
            throw e;
        }
    }
    log(`project discovery=${discoveryMs}ms client=${Date.now() - started}ms`);
    const selectedWorld = document.languageId === 'segusum' ? worldId(document) : '(C# target from source)';
    log(`selection document=${document.uri.fsPath} workspace=${folder.uri.fsPath} project=${projectPath} world=${selectedWorld}`);
    if (!client.isReady)
        throw new Error('Segusum host is not ready.');
    status.text = 'Segusum: Ready';
    status.tooltip = `Project: ${projectPath}\nWorld: ${selectedWorld}`;
    status.show();
    return client;
}
function position() { const editor = vscode.window.activeTextEditor; if (!editor)
    throw new Error('No active editor.'); return { editor, path: editor.document.uri.fsPath, line: editor.selection.active.line + 1, column: editor.selection.active.character + 1 }; }
async function savedPosition() { if (!await vscode.workspace.saveAll())
    throw new Error('Save was cancelled; no semantic operation was run.'); return position(); }
async function focusReferencesView() { try {
    await vscode.commands.executeCommand('segusum.referencesView.focus');
}
catch {
    await vscode.commands.executeCommand('workbench.view.extension.segusum.references');
} }
async function activate(context) {
    const activationStarted = Date.now();
    output = vscode.window.createOutputChannel('Segusum');
    context.subscriptions.push(output);
    status = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
    status.text = 'Segusum: Idle';
    status.tooltip = 'No Segusum project is loading.';
    status.show();
    context.subscriptions.push(status);
    if (!vscode.workspace.workspaceFolders?.length) {
        status.text = 'Segusum: No workspace';
        return;
    }
    log(`${BUILD_ID}`);
    log(`activation complete elapsed=${Date.now() - activationStarted}ms; host initialization is lazy.`);
    context.subscriptions.push(vscode.languages.registerDefinitionProvider({ language: 'segusum' }, { provideDefinition: async (document, pos) => { try {
            const client = await clientFor(document);
            const result = await client.request('definition', { path: document.uri.fsPath, line: pos.line + 1, column: pos.character + 1 });
            return result?.path ? new vscode.Location(vscode.Uri.file(result.path), new vscode.Position(result.line - 1, result.column - 1)) : undefined;
        }
        catch (e) {
            log(`Definition failed: ${e}`);
            output.show(true);
            vscode.window.showErrorMessage(`Segusum definition failed: ${e}`);
            return undefined;
        } } }));
    context.subscriptions.push(vscode.languages.registerCompletionItemProvider({ language: 'segusum' }, { provideCompletionItems: async (document, pos, token) => {
            log(`completion provider invoked document=${document.uri.fsPath} line=${pos.line + 1} column=${pos.character + 1}`);
            const key = document.uri.toString();
            completionCts.get(key)?.cancel();
            completionCts.get(key)?.dispose();
            const cts = new vscode.CancellationTokenSource();
            completionCts.set(key, cts);
            const subscription = token.onCancellationRequested(() => cts.cancel());
            const started = Date.now();
            try {
                const client = await clientFor(document);
                if (cts.token.isCancellationRequested)
                    return [];
                const result = await client.request('completion', { path: document.uri.fsPath, line: pos.line + 1, column: pos.character + 1, text: document.getText() }, cts.token);
                log(`completion document=${document.uri.fsPath} items=${(result ?? []).length} total=${Date.now() - started}ms`);
                return (result ?? []).map((item) => new vscode.CompletionItem(item.label));
            }
            catch (e) {
                if (!cts.token.isCancellationRequested)
                    log(`Completion failed: ${e}`);
                return [];
            }
            finally {
                subscription.dispose();
                if (completionCts.get(key) === cts)
                    completionCts.delete(key);
                cts.dispose();
            }
        } }, '.'));
    context.subscriptions.push(vscode.commands.registerCommand('segusum.findAllReferences', async () => { try {
        const query = await savedPosition();
        const client = await clientFor(query.editor.document);
        requestCts?.cancel();
        requestCts = new vscode.CancellationTokenSource();
        await vscode.window.withProgress({ location: vscode.ProgressLocation.Notification, title: 'Segusum references', cancellable: true }, async (_progress, token) => { token.onCancellationRequested(() => requestCts?.cancel()); const result = await client.request('references', { path: query.path, line: query.line, column: query.column }, requestCts.token); refs.clear(); for (const reference of result ?? []) {
            let preview = reference.displayName;
            try {
                const document = await vscode.workspace.openTextDocument(vscode.Uri.file(reference.path));
                preview = document.lineAt(Math.max(0, reference.line - 1)).text.trim().slice(0, 160);
            }
            catch { /* keep symbol name */ }
            const group = refs.get(reference.path) ?? [];
            group.push({ ...reference, preview });
            refs.set(reference.path, group);
        } referenceProvider.refresh(); await focusReferencesView(); if (!result?.length)
            vscode.window.showInformationMessage('No Segusum references found'); });
    }
    catch (e) {
        log(`References failed: ${e}`);
        output.show(true);
        vscode.window.showErrorMessage(`Segusum references failed: ${e}`);
    } }));
    context.subscriptions.push(vscode.commands.registerCommand('segusum.renameSymbol', async () => { try {
        const query = await savedPosition();
        const name = await vscode.window.showInputBox({ prompt: 'New symbol name' });
        if (!name)
            return;
        const client = await clientFor(query.editor.document);
        const result = await client.request('rename', { path: query.path, line: query.line, column: query.column, newName: name });
        if (!result.succeeded) {
            vscode.window.showErrorMessage((result.diagnostics ?? []).map((d) => `${d.id}: ${d.message}`).join('\n'));
            return;
        }
        const edit = new vscode.WorkspaceEdit();
        for (const item of result.edits)
            edit.replace(vscode.Uri.file(item.path), new vscode.Range(item.line - 1, item.column - 1, item.line - 1, item.column - 1 + item.length), item.newText);
        if (await vscode.workspace.applyEdit(edit))
            client.invalidate();
    }
    catch (e) {
        log(`Rename failed: ${e}`);
        output.show(true);
        vscode.window.showErrorMessage(`Segusum rename failed: ${e}`);
    } }));
    context.subscriptions.push(vscode.commands.registerCommand('segusum.openReference', async (item) => { const document = await vscode.workspace.openTextDocument(vscode.Uri.file(item.path)); const editor = await vscode.window.showTextDocument(document); editor.selection = new vscode.Selection(item.line - 1, item.column - 1, item.line - 1, item.column - 1 + item.length); editor.revealRange(editor.selection); }));
    context.subscriptions.push(vscode.window.registerTreeDataProvider('segusum.referencesView', referenceProvider));
    const invalidate = (uri, kind) => { if ((0, invalidation_1.isGeneratedPath)(uri.fsPath))
        return; log(`File ${kind} ${uri.fsPath}; invalidating clients.`); if (uri.fsPath.toLowerCase().endsWith('.csproj'))
        projectByFolder.clear(); for (const client of clients.values())
        client.invalidate(); };
    const watcher = vscode.workspace.createFileSystemWatcher('**/*.{seg,cs,csproj}');
    watcher.onDidChange(uri => invalidate(uri, 'changed'));
    watcher.onDidCreate(uri => invalidate(uri, 'created'));
    watcher.onDidDelete(uri => invalidate(uri, 'deleted'));
    context.subscriptions.push(watcher);
    context.subscriptions.push(vscode.workspace.onDidChangeWorkspaceFolders(() => {
        projectByFolder.clear();
        log('Workspace folders changed; cleared project discovery cache.');
    }));
    context.subscriptions.push(vscode.window.onDidChangeActiveTextEditor(editor => { if (editor) {
        status.text = 'Segusum: Idle';
        status.tooltip = `Project will load on semantic request\nDocument: ${editor.document.uri.fsPath}`;
        status.show();
        log(`Active document selected document=${editor.document.uri.fsPath}; semantic host start deferred.`);
    } }));
    const active = vscode.window.activeTextEditor;
    if (active && (active.document.languageId === 'segusum' || active.document.uri.fsPath.toLowerCase().endsWith('.cs'))) {
        status.text = 'Segusum: Loading';
        status.tooltip = `Document: ${active.document.uri.fsPath}\nPrewarming its consumer project...`;
        status.show();
        log(`Prewarm started document=${active.document.uri.fsPath}`);
        void clientFor(active.document).then(() => log(`Prewarm complete document=${active.document.uri.fsPath}`)).catch(e => { status.text = 'Segusum: Error'; status.tooltip = String(e); status.show(); log(`Prewarm failed: ${e}`); });
    }
}
function deactivate() { for (const client of clients.values())
    client.dispose(); requestCts?.dispose(); for (const cts of completionCts.values())
    cts.dispose(); }
//# sourceMappingURL=extension.js.map