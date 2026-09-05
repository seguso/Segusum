import * as vscode from 'vscode';
import { spawn, ChildProcessWithoutNullStreams } from 'child_process';
import * as path from 'path';
import * as fs from 'fs';

type RpcResponse = { id: number; result?: any; error?: { code: string; message: string } };
const BUILD_ID = 'extension build = completion-fastpath-2026-09-05';
let output: vscode.OutputChannel;
let status: vscode.StatusBarItem;
function log(message: string) { output?.appendLine(`[${new Date().toISOString()}] ${message}`); }

class HostClient {
  private child?: ChildProcessWithoutNullStreams;
  private next = 1;
  private pending = new Map<number, { resolve: (v:any)=>void; reject:(e:any)=>void }>();
  public worlds: any[] = [];
  private startTask?: Promise<void>;
  constructor(public readonly projectPath: string, private readonly workspacePath: string) {}
  async start(): Promise<void> {
    if (!this.startTask) this.startTask = this.startCore();
    return this.startTask;
  }
  private async startCore(): Promise<void> {
    const configured = vscode.workspace.getConfiguration('segusum').get<string>('toolingHostPath');
    const dll = configured || path.join(this.workspacePath, 'Segusum.Tooling.Host', 'bin', 'Debug', 'net8.0', 'Segusum.Tooling.Host.dll');
    log(`${BUILD_ID}`); log(`Starting host=${dll} project=${this.projectPath}`);
    if (!fs.existsSync(dll)) throw new Error(`Tooling host not found: ${dll}`);
    this.child = spawn('dotnet', [dll], { cwd: this.workspacePath, stdio: ['pipe', 'pipe', 'pipe'] });
    let buffer = '';
    this.child.stdout.on('data', data => { buffer += data.toString(); let end: number; while ((end = buffer.indexOf('\n')) >= 0) { const line = buffer.slice(0, end); buffer = buffer.slice(end + 1); if (!line.trim()) continue; try { const response = JSON.parse(line) as RpcResponse; const pending = this.pending.get(response.id); if (!pending) continue; this.pending.delete(response.id); response.error ? pending.reject(new Error(response.error.message)) : pending.resolve(response.result); } catch (e) { log(`Invalid host response: ${e}`); } } });
    this.child.stderr.on('data', data => log(`host stderr: ${data.toString().trim()}`));
    const initialized = await this.request('initialize', { projectPath: this.projectPath });
    this.worlds = initialized?.worlds ?? [];
    log(`Host initialized project=${initialized?.projectPath ?? this.projectPath} worlds=${this.worlds.map((x:any)=>x.id).join(',')}`);
  }
  request(method: string, params: any, token?: vscode.CancellationToken): Promise<any> { const id = this.next++; log(`RPC start #${id} ${method} project=${this.projectPath}`); return new Promise((resolve, reject) => { this.pending.set(id, { resolve: value => { log(`RPC end #${id} ${method}`); resolve(value); }, reject: error => { log(`RPC error #${id} ${method}: ${error}`); reject(error); } }); this.child?.stdin.write(JSON.stringify({ id, method, params }) + '\n'); if (token) token.onCancellationRequested(() => this.cancel(id)); }); }
  cancel(id: number): void { this.child?.stdin.write(JSON.stringify({ id: this.next++, method: 'cancel', params: { requestId: id } }) + '\n'); }
  invalidate(): void { log(`Invalidating project=${this.projectPath}`); void this.request('invalidate', {}).catch(e => log(`invalidate failed: ${e}`)); }
  dispose(): void { this.child?.kill(); this.child = undefined; for (const pending of this.pending.values()) pending.reject(new Error('Host stopped')); this.pending.clear(); }
}

let requestCts: vscode.CancellationTokenSource | undefined;
const clients = new Map<string, HostClient>();
const projectByFolder = new Map<string, string>();
const completionCts = new Map<string, vscode.CancellationTokenSource>();
const refs = new Map<string, any[]>();
class ReferenceNode extends vscode.TreeItem { constructor(public readonly value: any) { super(value.path ? `${value.line}: ${value.preview ?? value.displayName}` : value.file, value.path ? vscode.TreeItemCollapsibleState.None : vscode.TreeItemCollapsibleState.Expanded); this.description = value.language; if (value.path) this.command = { command: 'segusum.openReference', title: 'Open reference', arguments: [value] }; } }
class ReferenceProvider implements vscode.TreeDataProvider<ReferenceNode> { private change = new vscode.EventEmitter<void>(); readonly onDidChangeTreeData = this.change.event; getTreeItem(node: ReferenceNode) { return node; } getChildren(node?: ReferenceNode) { if (node) return (refs.get(node.value.file) ?? []).map(x => new ReferenceNode(x)); return [...refs.keys()].sort((a, b) => a.localeCompare(b)).map(file => new ReferenceNode({ file })); } refresh() { this.change.fire(); } }
const referenceProvider = new ReferenceProvider();

async function discoverProject(folder: vscode.WorkspaceFolder): Promise<string | undefined> {
  const files = await vscode.workspace.findFiles(new vscode.RelativePattern(folder, '**/*.csproj'), '**/{bin,obj,node_modules}/**');
  const segs = await vscode.workspace.findFiles(new vscode.RelativePattern(folder, '**/*.seg'), '**/{bin,obj,node_modules}/**');
  return files.filter(file => segs.some(seg => seg.fsPath.startsWith(path.dirname(file.fsPath) + path.sep))).sort((a, b) => a.fsPath.localeCompare(b.fsPath))[0]?.fsPath;
}
function folderFor(document: vscode.TextDocument): vscode.WorkspaceFolder { const folder = vscode.workspace.getWorkspaceFolder(document.uri); if (!folder) throw new Error(`No workspace folder owns ${document.uri.fsPath}`); return folder; }
function worldId(document: vscode.TextDocument): string { const match = document.getText().match(/^\s*world\s+([A-Za-z_][A-Za-z0-9_-]*)/m); return match?.[1] ?? '(unknown)'; }
async function clientFor(document: vscode.TextDocument): Promise<HostClient> {
  const started = Date.now();
  const folder = folderFor(document); const folderKey = folder.uri.toString();
  let projectPath = projectByFolder.get(folderKey);
  if (!projectPath) {
    projectPath = await discoverProject(folder);
    if (projectPath) projectByFolder.set(folderKey, projectPath);
  }
  if (!projectPath) throw new Error(`No consumer .csproj containing .seg files found under ${folder.uri.fsPath}`);
  const discoveryMs = Date.now() - started;
  const key = path.normalize(projectPath).toLowerCase(); let client = clients.get(key);
  if (!client) { client = new HostClient(projectPath, folder.uri.fsPath); clients.set(key, client); try { await client.start(); } catch (e) { clients.delete(key); client.dispose(); throw e; } }
  log(`project discovery=${discoveryMs}ms client=${Date.now() - started}ms`);
  const selectedWorld = document.languageId === 'segusum' ? worldId(document) : '(C# target from source)';
  log(`selection document=${document.uri.fsPath} workspace=${folder.uri.fsPath} project=${projectPath} world=${selectedWorld}`);
  status.text = 'Segusum: Ready'; status.tooltip = `Project: ${projectPath}\nWorld: ${selectedWorld}`; status.show();
  return client;
}
function position() { const editor = vscode.window.activeTextEditor; if (!editor) throw new Error('No active editor.'); return { editor, path: editor.document.uri.fsPath, line: editor.selection.active.line + 1, column: editor.selection.active.character + 1 }; }
async function savedPosition() { if (!await vscode.workspace.saveAll()) throw new Error('Save was cancelled; no semantic operation was run.'); return position(); }
async function focusReferencesView() { try { await vscode.commands.executeCommand('segusum.referencesView.focus'); } catch { await vscode.commands.executeCommand('workbench.view.extension.segusum.references'); } }

export async function activate(context: vscode.ExtensionContext) {
  output = vscode.window.createOutputChannel('Segusum'); context.subscriptions.push(output);
  status = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100); status.text = 'Segusum: Starting'; status.show(); context.subscriptions.push(status);
  if (!vscode.workspace.workspaceFolders?.length) { status.text = 'Segusum: No workspace'; return; }
  log(`${BUILD_ID}`);
  context.subscriptions.push(vscode.languages.registerDefinitionProvider({ language: 'segusum' }, { provideDefinition: async (document, pos) => { try { const client = await clientFor(document); const result = await client.request('definition', { path: document.uri.fsPath, line: pos.line + 1, column: pos.character + 1, text: document.getText() }); return result?.path ? new vscode.Location(vscode.Uri.file(result.path), new vscode.Position(result.line - 1, result.column - 1)) : undefined; } catch (e) { log(`Definition failed: ${e}`); output.show(true); vscode.window.showErrorMessage(`Segusum definition failed: ${e}`); return undefined; } } }));
  context.subscriptions.push(vscode.languages.registerCompletionItemProvider({ language: 'segusum' }, { provideCompletionItems: async (document, pos, token) => {
    const key = document.uri.toString();
    completionCts.get(key)?.cancel(); completionCts.get(key)?.dispose();
    const cts = new vscode.CancellationTokenSource(); completionCts.set(key, cts);
    const subscription = token.onCancellationRequested(() => cts.cancel());
    const started = Date.now();
    try {
      const client = await clientFor(document);
      if (cts.token.isCancellationRequested) return [];
      const result = await client.request('completion', { path: document.uri.fsPath, line: pos.line + 1, column: pos.character + 1, text: document.getText() }, cts.token);
      log(`completion document=${document.uri.fsPath} total=${Date.now() - started}ms`);
      return (result ?? []).map((item: any) => new vscode.CompletionItem(item.label));
    } catch (e) { if (!cts.token.isCancellationRequested) log(`Completion failed: ${e}`); return []; }
    finally { subscription.dispose(); if (completionCts.get(key) === cts) completionCts.delete(key); cts.dispose(); }
  } }, '.'));
  context.subscriptions.push(vscode.commands.registerCommand('segusum.findAllReferences', async () => { try { const query = await savedPosition(); const client = await clientFor(query.editor.document); requestCts?.cancel(); requestCts = new vscode.CancellationTokenSource(); await vscode.window.withProgress({ location: vscode.ProgressLocation.Notification, title: 'Segusum references', cancellable: true }, async (_progress, token) => { token.onCancellationRequested(() => requestCts?.cancel()); const result = await client.request('references', { path: query.path, line: query.line, column: query.column }, requestCts!.token); refs.clear(); for (const reference of result ?? []) { let preview = reference.displayName; try { const document = await vscode.workspace.openTextDocument(vscode.Uri.file(reference.path)); preview = document.lineAt(Math.max(0, reference.line - 1)).text.trim().slice(0, 160); } catch { /* keep symbol name */ } const group = refs.get(reference.path) ?? []; group.push({ ...reference, preview }); refs.set(reference.path, group); } referenceProvider.refresh(); await focusReferencesView(); if (!result?.length) vscode.window.showInformationMessage('No Segusum references found'); }); } catch (e) { log(`References failed: ${e}`); output.show(true); vscode.window.showErrorMessage(`Segusum references failed: ${e}`); } }));
  context.subscriptions.push(vscode.commands.registerCommand('segusum.renameSymbol', async () => { try { const query = await savedPosition(); const name = await vscode.window.showInputBox({ prompt: 'New symbol name' }); if (!name) return; const client = await clientFor(query.editor.document); const result = await client.request('rename', { path: query.path, line: query.line, column: query.column, newName: name }); if (!result.succeeded) { vscode.window.showErrorMessage((result.diagnostics ?? []).map((d: any) => `${d.id}: ${d.message}`).join('\n')); return; } const edit = new vscode.WorkspaceEdit(); for (const item of result.edits) edit.replace(vscode.Uri.file(item.path), new vscode.Range(item.line - 1, item.column - 1, item.line - 1, item.column - 1 + item.length), item.newText); if (await vscode.workspace.applyEdit(edit)) client.invalidate(); } catch (e) { log(`Rename failed: ${e}`); output.show(true); vscode.window.showErrorMessage(`Segusum rename failed: ${e}`); } }));
  context.subscriptions.push(vscode.commands.registerCommand('segusum.openReference', async (item: any) => { const document = await vscode.workspace.openTextDocument(vscode.Uri.file(item.path)); const editor = await vscode.window.showTextDocument(document); editor.selection = new vscode.Selection(item.line - 1, item.column - 1, item.line - 1, item.column - 1 + item.length); editor.revealRange(editor.selection); }));
  context.subscriptions.push(vscode.window.registerTreeDataProvider('segusum.referencesView', referenceProvider));
  const invalidate = (uri: vscode.Uri, kind: string) => { log(`File ${kind} ${uri.fsPath}; invalidating clients.`); if (uri.fsPath.toLowerCase().endsWith('.csproj')) projectByFolder.clear(); for (const client of clients.values()) client.invalidate(); };
  const watcher = vscode.workspace.createFileSystemWatcher('**/*.{seg,cs,csproj}'); watcher.onDidChange(uri => invalidate(uri, 'changed')); watcher.onDidCreate(uri => invalidate(uri, 'created')); watcher.onDidDelete(uri => invalidate(uri, 'deleted')); context.subscriptions.push(watcher);
  context.subscriptions.push(vscode.workspace.onDidChangeWorkspaceFolders(() => {
    projectByFolder.clear();
    log('Workspace folders changed; cleared project discovery cache.');
  }));
  context.subscriptions.push(vscode.window.onDidChangeActiveTextEditor(editor => { if (editor) void clientFor(editor.document).catch(e => log(`Active document selection failed: ${e}`)); }));
}
export function deactivate() { for (const client of clients.values()) client.dispose(); requestCts?.dispose(); for (const cts of completionCts.values()) cts.dispose(); }
