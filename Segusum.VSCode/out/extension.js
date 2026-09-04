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
class HostClient {
    child;
    next = 1;
    pending = new Map();
    async start(root) {
        const configured = vscode.workspace.getConfiguration('segusum').get('toolingHostPath');
        const dll = configured || path.join(root, 'Segusum.Tooling.Host', 'bin', 'Debug', 'net8.0', 'Segusum.Tooling.Host.dll');
        if (!fs.existsSync(dll))
            throw new Error(`Tooling host not found: ${dll}`);
        this.child = (0, child_process_1.spawn)('dotnet', [dll], { cwd: root, stdio: ['pipe', 'pipe', 'pipe'] });
        let buffer = '';
        this.child.stdout.on('data', data => { buffer += data.toString(); let e; while ((e = buffer.indexOf('\n')) >= 0) {
            const line = buffer.slice(0, e);
            buffer = buffer.slice(e + 1);
            if (!line.trim())
                continue;
            try {
                const r = JSON.parse(line);
                const p = this.pending.get(r.id);
                if (!p)
                    continue;
                this.pending.delete(r.id);
                r.error ? p.reject(new Error(r.error.message)) : p.resolve(r.result);
            }
            catch { /* protocol remains stdout-only JSON */ }
        } });
        this.child.stderr.on('data', data => console.error(`[Segusum] ${data}`));
        await this.request('initialize', { projectPath: await discoverProject(root) });
    }
    request(method, params, token) { const id = this.next++; return new Promise((resolve, reject) => { this.pending.set(id, { resolve, reject }); this.child?.stdin.write(JSON.stringify({ id, method, params }) + '\n'); if (token)
        token.onCancellationRequested(() => this.cancel(id)); }); }
    cancel(id) { this.child?.stdin.write(JSON.stringify({ id: this.next++, method: 'cancel', params: { requestId: id } }) + '\n'); }
    invalidate() { void this.request('invalidate', {}); }
    dispose() { this.child?.kill(); this.child = undefined; for (const p of this.pending.values())
        p.reject(new Error('Host stopped')); this.pending.clear(); }
}
let client;
let requestCts;
const refs = new Map();
class ReferenceNode extends vscode.TreeItem {
    value;
    parent;
    constructor(value, parent) {
        super(value.path ? `${value.line}: ${value.preview ?? value.displayName}` : value.file, value.path ? vscode.TreeItemCollapsibleState.None : vscode.TreeItemCollapsibleState.Expanded);
        this.value = value;
        this.parent = parent;
        this.description = value.language;
        if (value.path)
            this.command = { command: 'segusum.openReference', title: 'Open reference', arguments: [value] };
    }
}
class ReferenceProvider {
    change = new vscode.EventEmitter();
    onDidChangeTreeData = this.change.event;
    getTreeItem(n) { return n; }
    getChildren(n) { if (n)
        return (refs.get(n.value.file) ?? []).map(x => new ReferenceNode(x)); return [...refs.entries()].map(([file]) => new ReferenceNode({ file })); }
    refresh() { this.change.fire(); }
}
const referenceProvider = new ReferenceProvider();
async function discoverProject(root) { const files = await vscode.workspace.findFiles('**/*.csproj', '**/{bin,obj,node_modules}/**'); const seg = await vscode.workspace.findFiles('**/*.seg', '**/{bin,obj,node_modules}/**'); const hit = files.find(f => seg.some(s => s.fsPath.startsWith(path.dirname(f.fsPath) + path.sep))); return hit?.fsPath; }
function position() { const e = vscode.window.activeTextEditor; if (!e)
    throw new Error('No active editor.'); return { editor: e, path: e.document.uri.fsPath, line: e.selection.active.line + 1, column: e.selection.active.character + 1 }; }
async function savedPosition() { if (!await vscode.workspace.saveAll())
    throw new Error('Save was cancelled; no semantic operation was run.'); return position(); }
async function call(method, params, token) { if (!client)
    throw new Error('Segusum host is not initialized.'); if (token?.isCancellationRequested)
    throw new vscode.CancellationError(); return client.request(method, params, token); }
async function activate(context) {
    const root = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
    if (!root)
        return;
    client = new HostClient();
    try {
        await client.start(root);
    }
    catch (e) {
        vscode.window.showErrorMessage(String(e));
        return;
    }
    context.subscriptions.push(vscode.languages.registerDefinitionProvider({ language: 'segusum' }, { provideDefinition: async (_d, p) => { const x = await call('definition', { path: _d.uri.fsPath, line: p.line + 1, column: p.character + 1 }); return x?.path ? new vscode.Location(vscode.Uri.file(x.path), new vscode.Position(x.line - 1, x.column - 1)) : undefined; } }));
    context.subscriptions.push(vscode.languages.registerCompletionItemProvider({ language: 'segusum' }, { provideCompletionItems: async (d, p) => { const x = await call('completion', { path: d.uri.fsPath, line: p.line + 1, column: p.character + 1 }); return (x ?? []).map((a) => new vscode.CompletionItem(a.label)); } }, '.'));
    context.subscriptions.push(vscode.commands.registerCommand('segusum.findAllReferences', async () => { const q = await savedPosition(); requestCts?.cancel(); requestCts = new vscode.CancellationTokenSource(); await vscode.window.withProgress({ location: vscode.ProgressLocation.Notification, title: 'Segusum references', cancellable: true }, async (_p, t) => { t.onCancellationRequested(() => requestCts?.cancel()); const result = await call('references', { path: q.path, line: q.line, column: q.column }, requestCts.token); refs.clear(); for (const r of result ?? []) {
        let preview = r.displayName;
        try {
            const d = await vscode.workspace.openTextDocument(vscode.Uri.file(r.path));
            preview = d.lineAt(Math.max(0, r.line - 1)).text.trim().slice(0, 160);
        }
        catch { }
        const a = refs.get(r.path) ?? [];
        a.push({ ...r, preview });
        refs.set(r.path, a);
    } referenceProvider.refresh(); }); }));
    context.subscriptions.push(vscode.commands.registerCommand('segusum.renameSymbol', async () => { const q = await savedPosition(); const name = await vscode.window.showInputBox({ prompt: 'New symbol name' }); if (!name)
        return; try {
        const r = await call('rename', { path: q.path, line: q.line, column: q.column, newName: name });
        if (!r.succeeded) {
            vscode.window.showErrorMessage((r.diagnostics ?? []).map((d) => `${d.id}: ${d.message}`).join('\n'));
            return;
        }
        const edit = new vscode.WorkspaceEdit();
        for (const x of r.edits)
            edit.replace(vscode.Uri.file(x.path), new vscode.Range(x.line - 1, x.column - 1, x.line - 1, x.column - 1 + x.length), x.newText);
        if (await vscode.workspace.applyEdit(edit)) {
            client?.invalidate();
        }
    }
    catch (e) {
        vscode.window.showErrorMessage(String(e));
    } }));
    context.subscriptions.push(vscode.commands.registerCommand('segusum.openReference', async (x) => { const d = await vscode.workspace.openTextDocument(vscode.Uri.file(x.path)); const ed = await vscode.window.showTextDocument(d); ed.selection = new vscode.Selection(x.line - 1, x.column - 1, x.line - 1, x.column - 1 + x.length); ed.revealRange(ed.selection); }));
    context.subscriptions.push(vscode.window.registerTreeDataProvider('segusum.referencesView', referenceProvider));
    const watcher = vscode.workspace.createFileSystemWatcher('**/*.{seg,cs}');
    watcher.onDidChange(() => client?.invalidate());
    watcher.onDidCreate(() => client?.invalidate());
    watcher.onDidDelete(() => client?.invalidate());
    context.subscriptions.push(watcher);
}
function deactivate() { client?.dispose(); requestCts?.dispose(); }
//# sourceMappingURL=extension.js.map