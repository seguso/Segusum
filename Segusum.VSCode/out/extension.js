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
const BUILD_ID = 'extension build = multi-root-routing-2026-09-05';
let output;
let status;
function log(message) { output?.appendLine(`[${new Date().toISOString()}] ${message}`); }
class HostClient {
    projectPath;
    workspacePath;
    child;
    next = 1;
    pending = new Map();
    worlds = [];
    constructor(projectPath, workspacePath) {
        this.projectPath = projectPath;
        this.workspacePath = workspacePath;
    }
    async start() {
        const configured = vscode.workspace.getConfiguration('segusum').get('toolingHostPath');
        const dll = configured || path.join(this.workspacePath, 'Segusum.Tooling.Host', 'bin', 'Debug', 'net8.0', 'Segusum.Tooling.Host.dll');
        log(`${BUILD_ID}`);
        log(`Starting host=${dll} project=${this.projectPath}`);
        if (!fs.existsSync(dll))
            throw new Error(`Tooling host not found: ${dll}`);
        this.child = (0, child_process_1.spawn)('dotnet', [dll], { cwd: this.workspacePath, stdio: ['pipe', 'pipe', 'pipe'] });
        let buffer = '';
        this.child.stdout.on('data', data => { buffer += data.toString(); let end; while ((end = buffer.indexOf('\n')) >= 0) {
            const line = buffer.slice(0, end);
            buffer = buffer.slice(end + 1);
            if (!line.trim())
                continue;
            try {
                const response = JSON.parse(line);
                const pending = this.pending.get(response.id);
                if (!pending)
                    continue;
                this.pending.delete(response.id);
                response.error ? pending.reject(new Error(response.error.message)) : pending.resolve(response.result);
            }
            catch (e) {
                log(`Invalid host response: ${e}`);
            }
        } });
        this.child.stderr.on('data', data => log(`host stderr: ${data.toString().trim()}`));
        const initialized = await this.request('initialize', { projectPath: this.projectPath });
        this.worlds = initialized?.worlds ?? [];
        log(`Host initialized project=${initialized?.projectPath ?? this.projectPath} worlds=${this.worlds.map((x) => x.id).join(',')}`);
    }
    request(method, params, token) { const id = this.next++; log(`RPC start #${id} ${method} project=${this.projectPath}`); return new Promise((resolve, reject) => { this.pending.set(id, { resolve: value => { log(`RPC end #${id} ${method}`); resolve(value); }, reject: error => { log(`RPC error #${id} ${method}: ${error}`); reject(error); } }); this.child?.stdin.write(JSON.stringify({ id, method, params }) + '\n'); if (token)
        token.onCancellationRequested(() => this.cancel(id)); }); }
    cancel(id) { this.child?.stdin.write(JSON.stringify({ id: this.next++, method: 'cancel', params: { requestId: id } }) + '\n'); }
    invalidate() { log(`Invalidating project=${this.projectPath}`); void this.request('invalidate', {}).catch(e => log(`invalidate failed: ${e}`)); }
    dispose() { this.child?.kill(); this.child = undefined; for (const pending of this.pending.values())
        pending.reject(new Error('Host stopped')); this.pending.clear(); }
}
let requestCts;
const clients = new Map();
const refs = new Map();
class ReferenceNode extends vscode.TreeItem {
    value;
    constructor(value) {
        super(value.path ? `${value.line}: ${value.preview ?? value.displayName}` : value.file, value.path ? vscode.TreeItemCollapsibleState.None : vscode.TreeItemCollapsibleState.Expanded);
        this.value = value;
        this.description = value.language;
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
    const folder = folderFor(document);
    const projectPath = await discoverProject(folder);
    if (!projectPath)
        throw new Error(`No consumer .csproj containing .seg files found under ${folder.uri.fsPath}`);
    const key = path.normalize(projectPath).toLowerCase();
    let client = clients.get(key);
    if (!client) {
        client = new HostClient(projectPath, folder.uri.fsPath);
        clients.set(key, client);
        try {
            await client.start();
        }
        catch (e) {
            clients.delete(key);
            client.dispose();
            throw e;
        }
    }
    const selectedWorld = document.languageId === 'segusum' ? worldId(document) : '(C# target from source)';
    log(`selection document=${document.uri.fsPath} workspace=${folder.uri.fsPath} project=${projectPath} world=${selectedWorld}`);
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
    output = vscode.window.createOutputChannel('Segusum');
    context.subscriptions.push(output);
    status = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
    status.text = 'Segusum: Starting';
    status.show();
    context.subscriptions.push(status);
    if (!vscode.workspace.workspaceFolders?.length) {
        status.text = 'Segusum: No workspace';
        return;
    }
    log(`${BUILD_ID}`);
    context.subscriptions.push(vscode.languages.registerDefinitionProvider({ language: 'segusum' }, { provideDefinition: async (document, pos) => { try {
            const client = await clientFor(document);
            const result = await client.request('definition', { path: document.uri.fsPath, line: pos.line + 1, column: pos.character + 1, text: document.getText() });
            return result?.path ? new vscode.Location(vscode.Uri.file(result.path), new vscode.Position(result.line - 1, result.column - 1)) : undefined;
        }
        catch (e) {
            log(`Definition failed: ${e}`);
            output.show(true);
            vscode.window.showErrorMessage(`Segusum definition failed: ${e}`);
            return undefined;
        } } }));
    context.subscriptions.push(vscode.languages.registerCompletionItemProvider({ language: 'segusum' }, { provideCompletionItems: async (document, pos) => { try {
            const client = await clientFor(document);
            const result = await client.request('completion', { path: document.uri.fsPath, line: pos.line + 1, column: pos.character + 1, text: document.getText() });
            return (result ?? []).map((item) => new vscode.CompletionItem(item.label));
        }
        catch (e) {
            log(`Completion failed: ${e}`);
            return [];
        } } }, '.'));
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
    const watcher = vscode.workspace.createFileSystemWatcher('**/*.{seg,cs}');
    watcher.onDidChange(uri => { log(`File changed ${uri.fsPath}; invalidating clients.`); for (const client of clients.values())
        client.invalidate(); });
    watcher.onDidCreate(uri => { log(`File created ${uri.fsPath}; invalidating clients.`); for (const client of clients.values())
        client.invalidate(); });
    watcher.onDidDelete(uri => { log(`File deleted ${uri.fsPath}; invalidating clients.`); for (const client of clients.values())
        client.invalidate(); });
    context.subscriptions.push(watcher);
    context.subscriptions.push(vscode.window.onDidChangeActiveTextEditor(editor => { if (editor)
        void clientFor(editor.document).catch(e => log(`Active document selection failed: ${e}`)); }));
}
function deactivate() { for (const client of clients.values())
    client.dispose(); requestCts?.dispose(); }
//# sourceMappingURL=extension.js.map