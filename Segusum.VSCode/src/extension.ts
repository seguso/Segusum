import * as vscode from 'vscode';
import { spawn, ChildProcessWithoutNullStreams } from 'child_process';
import * as path from 'path';
import * as fs from 'fs';

type RpcResponse = { id: number; result?: any; error?: { code: string; message: string } };
let output: vscode.OutputChannel;
let status: vscode.StatusBarItem;
function log(message: string) { output?.appendLine(`[${new Date().toISOString()}] ${message}`); }
class HostClient {
  private child?: ChildProcessWithoutNullStreams; private next = 1; private pending = new Map<number, { resolve: (v:any)=>void; reject:(e:any)=>void }>();
  async start(root: string): Promise<void> {
    const configured = vscode.workspace.getConfiguration('segusum').get<string>('toolingHostPath');
    const dll = configured || path.join(root, 'Segusum.Tooling.Host', 'bin', 'Debug', 'net8.0', 'Segusum.Tooling.Host.dll');
    log(`Starting host: ${dll}`); if (!fs.existsSync(dll)) throw new Error(`Tooling host not found: ${dll}`);
    this.child = spawn('dotnet', [dll], { cwd: root, stdio: ['pipe', 'pipe', 'pipe'] });
    let buffer = '';
    this.child.stdout.on('data', data => { buffer += data.toString(); let e; while ((e = buffer.indexOf('\n')) >= 0) { const line = buffer.slice(0,e); buffer = buffer.slice(e+1); if (!line.trim()) continue; try { const r = JSON.parse(line) as RpcResponse; const p = this.pending.get(r.id); if (!p) continue; this.pending.delete(r.id); r.error ? p.reject(new Error(r.error.message)) : p.resolve(r.result); } catch { /* protocol remains stdout-only JSON */ } } });
    this.child.stderr.on('data', data => log(`host stderr: ${data.toString().trim()}`));
    const projectPath = await discoverProject(root); log(`Initializing project: ${projectPath ?? '(discovery failed)'}`);
    await this.request('initialize', { projectPath }); log('Host initialized.');
  }
  request(method: string, params: any, token?: vscode.CancellationToken): Promise<any> { const id = this.next++; log(`RPC start #${id} ${method}`); return new Promise((resolve,reject) => { this.pending.set(id,{resolve:(v)=>{log(`RPC end #${id} ${method}`);resolve(v);},reject:(e)=>{log(`RPC error #${id} ${method}: ${e}`);reject(e);}}); this.child?.stdin.write(JSON.stringify({id,method,params})+'\n'); if (token) token.onCancellationRequested(() => this.cancel(id)); }); }
  cancel(id: number): void { this.child?.stdin.write(JSON.stringify({id: this.next++, method:'cancel', params:{requestId:id}})+'\n'); }
  invalidate(): void { log('Invalidating semantic workspace.'); void this.request('invalidate', {}).catch(e=>log(`invalidate failed: ${e}`)); }
  dispose(): void { this.child?.kill(); this.child=undefined; for (const p of this.pending.values()) p.reject(new Error('Host stopped')); this.pending.clear(); }
}
let client: HostClient | undefined; let requestCts: vscode.CancellationTokenSource | undefined;
const refs = new Map<string, any[]>();
class ReferenceNode extends vscode.TreeItem { constructor(public readonly value: any, public readonly parent?: ReferenceNode) { super(value.path ? `${value.line}: ${value.preview ?? value.displayName}` : value.file, value.path ? vscode.TreeItemCollapsibleState.None : vscode.TreeItemCollapsibleState.Expanded); this.description = value.language; if (value.path) this.command = { command:'segusum.openReference', title:'Open reference', arguments:[value] }; } }
class ReferenceProvider implements vscode.TreeDataProvider<ReferenceNode> { private change = new vscode.EventEmitter<void>(); readonly onDidChangeTreeData = this.change.event; getTreeItem(n:ReferenceNode){return n;} getChildren(n?:ReferenceNode){ if(n) return (refs.get(n.value.file)??[]).map(x=>new ReferenceNode(x)); return [...refs.entries()].map(([file])=>new ReferenceNode({file})); } refresh(){this.change.fire();} }
const referenceProvider = new ReferenceProvider();
async function discoverProject(root:string):Promise<string|undefined>{ const files=await vscode.workspace.findFiles('**/*.csproj','**/{bin,obj,node_modules}/**'); const seg=await vscode.workspace.findFiles('**/*.seg','**/{bin,obj,node_modules}/**'); const hit=files.find(f=>seg.some(s=>s.fsPath.startsWith(path.dirname(f.fsPath)+path.sep))); return hit?.fsPath; }
function position(){ const e=vscode.window.activeTextEditor; if(!e) throw new Error('No active editor.'); return { editor:e, path:e.document.uri.fsPath, line:e.selection.active.line+1, column:e.selection.active.character+1 }; }
async function savedPosition(){ if(!await vscode.workspace.saveAll()) throw new Error('Save was cancelled; no semantic operation was run.'); return position(); }
async function call(method:string, params:any, token?:vscode.CancellationToken){ if(!client) throw new Error('Segusum host is not initialized.'); if(token?.isCancellationRequested) throw new vscode.CancellationError(); return client.request(method,params,token); }
export async function activate(context:vscode.ExtensionContext){
  output=vscode.window.createOutputChannel('Segusum'); context.subscriptions.push(output); status=vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left,100); status.text='Segusum: Starting'; status.show(); context.subscriptions.push(status);
  const root=vscode.workspace.workspaceFolders?.[0]?.uri.fsPath; if(!root) { status.text='Segusum: No workspace'; return; } client=new HostClient(); try{await client.start(root); status.text='Segusum: Ready'; status.tooltip=`Project: ${root}`;}catch(e){status.text='Segusum: Error'; log(`Initialization failed: ${e}`); output.show(true); vscode.window.showErrorMessage(String(e)); return;}
  context.subscriptions.push(vscode.languages.registerDefinitionProvider({language:'segusum'}, {provideDefinition:async(_d,p)=>{try{const x=await call('definition',{path:_d.uri.fsPath,line:p.line+1,column:p.character+1}); return x?.path?new vscode.Location(vscode.Uri.file(x.path),new vscode.Position(x.line-1,x.column-1)):undefined;}catch(e){log(`Definition failed: ${e}`); output.show(true); vscode.window.showErrorMessage(`Segusum definition failed: ${e}`); return undefined;}}}));
  context.subscriptions.push(vscode.languages.registerCompletionItemProvider({language:'segusum'}, {provideCompletionItems:async(d,p)=>{try{const x=await call('completion',{path:d.uri.fsPath,line:p.line+1,column:p.character+1}); return (x??[]).map((a:any)=>new vscode.CompletionItem(a.label));}catch(e){log(`Completion failed: ${e}`); return [];}}}));
  context.subscriptions.push(vscode.commands.registerCommand('segusum.findAllReferences',async()=>{const q=await savedPosition(); requestCts?.cancel(); requestCts=new vscode.CancellationTokenSource(); await vscode.window.withProgress({location:vscode.ProgressLocation.Notification,title:'Segusum references',cancellable:true},async(_p,t)=>{t.onCancellationRequested(()=>requestCts?.cancel()); const result=await call('references',{path:q.path,line:q.line,column:q.column},requestCts!.token); refs.clear(); for(const r of result??[]){let preview=r.displayName; try{const d=await vscode.workspace.openTextDocument(vscode.Uri.file(r.path)); preview=d.lineAt(Math.max(0,r.line-1)).text.trim().slice(0,160);}catch{} const a=refs.get(r.path)??[]; a.push({ ...r, preview }); refs.set(r.path,a);} referenceProvider.refresh();});}));
  context.subscriptions.push(vscode.commands.registerCommand('segusum.renameSymbol',async()=>{const q=await savedPosition(); const name=await vscode.window.showInputBox({prompt:'New symbol name'}); if(!name)return; try{const r=await call('rename',{path:q.path,line:q.line,column:q.column,newName:name}); if(!r.succeeded){vscode.window.showErrorMessage((r.diagnostics??[]).map((d:any)=>`${d.id}: ${d.message}`).join('\n'));return;} const edit=new vscode.WorkspaceEdit(); for(const x of r.edits)edit.replace(vscode.Uri.file(x.path),new vscode.Range(x.line-1,x.column-1,x.line-1,x.column-1+x.length),x.newText); if(await vscode.workspace.applyEdit(edit)){client?.invalidate();}}catch(e){vscode.window.showErrorMessage(String(e));}}));
  context.subscriptions.push(vscode.commands.registerCommand('segusum.openReference',async(x:any)=>{const d=await vscode.workspace.openTextDocument(vscode.Uri.file(x.path)); const ed=await vscode.window.showTextDocument(d); ed.selection=new vscode.Selection(x.line-1,x.column-1,x.line-1,x.column-1+x.length); ed.revealRange(ed.selection); }));
  context.subscriptions.push(vscode.window.registerTreeDataProvider('segusum.referencesView',referenceProvider));
  const watcher=vscode.workspace.createFileSystemWatcher('**/*.{seg,cs}'); watcher.onDidChange(()=>client?.invalidate()); watcher.onDidCreate(()=>client?.invalidate()); watcher.onDidDelete(()=>client?.invalidate()); context.subscriptions.push(watcher);
}
export function deactivate(){client?.dispose(); requestCts?.dispose();}
