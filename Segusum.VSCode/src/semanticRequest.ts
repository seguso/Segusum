export type SemanticDocumentSnapshot = {
  path: string;
  line: number;
  column: number;
  text: string;
};

/** The text sent with every read-only semantic query is the editor snapshot. */
export function semanticDocumentSnapshot(path: string, line: number, column: number, text: string): SemanticDocumentSnapshot {
  return { path, line, column, text };
}
