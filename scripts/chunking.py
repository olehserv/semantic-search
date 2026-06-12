"""Code-aware chunking for C# files (production plan task 2.4).

Cuts on type and member borders with tree-sitter instead of sentence
windows, keeps signatures together with their bodies, and tags every
chunk with namespace/type/member metadata. Callers fall back to their
default splitter when chunk_csharp() returns None (source that does not
parse as C#).
"""
import tree_sitter_c_sharp
from tree_sitter import Language, Parser

# Roughly the 800-token budget the SentenceSplitter chunks used.
MAX_CHUNK_CHARS = 3200

TYPE_NODES = {
    "class_declaration",
    "interface_declaration",
    "struct_declaration",
    "enum_declaration",
    "record_declaration",
    "delegate_declaration",
}

_parser = Parser(Language(tree_sitter_c_sharp.language()))


def chunk_csharp(source):
    """Split C# source into code-aware chunks.

    Returns [(chunk_text, metadata), ...] with metadata keys "namespace",
    "type_name" and "members", or None when the source has parse errors
    (the caller should fall back to its default splitter).
    """
    tree = _parser.parse(source.encode("utf-8"))
    if tree.root_node.has_error:
        return None
    chunks = []
    _walk(tree.root_node, namespace="", chunks=chunks)
    return chunks or None


def _walk(container, namespace, chunks):
    """Visit a compilation unit or namespace body and chunk every type."""
    current_ns = namespace
    for child in container.named_children:
        if child.type == "namespace_declaration":
            ns = _qualify(current_ns, _name_of(child))
            body = child.child_by_field_name("body")
            if body is not None:
                _walk(body, ns, chunks)
        elif child.type == "file_scoped_namespace_declaration":
            # Applies to all following siblings, not to children of its own.
            current_ns = _qualify(current_ns, _name_of(child))
        elif child.type in TYPE_NODES:
            _chunk_type(child, current_ns, chunks)
        # using directives, comments etc. carry no search value on their own.


def _chunk_type(node, namespace, chunks):
    text = node.text.decode("utf-8")
    type_name = _name_of(node)
    prefix = f"// namespace {namespace}\n" if namespace else ""

    if len(prefix) + len(text) <= MAX_CHUNK_CHARS:
        chunks.append((prefix + text, _meta(namespace, type_name)))
        return

    body = node.child_by_field_name("body")
    if body is None:
        # A huge body-less declaration is practically impossible, but stay safe.
        _emit_split(prefix, text, _meta(namespace, type_name), chunks)
        return

    # Signature (attributes, modifiers, name, bases) stays on every chunk so
    # each piece still says which type it belongs to.
    signature = node.text[: body.start_byte - node.start_byte].decode("utf-8").rstrip()
    header = f"{prefix}{signature}\n{{\n"

    batch, batch_names = [], []

    def flush():
        if batch:
            chunk_text = header + "\n\n".join(batch) + "\n}"
            chunks.append(
                (chunk_text, _meta(namespace, type_name, ", ".join(batch_names)))
            )
            batch.clear()
            batch_names.clear()

    for member in body.named_children:
        if member.type == "comment":
            continue
        member_text = member.text.decode("utf-8")
        member_name = _name_of(member) or member.type

        if member.type in TYPE_NODES and len(member_text) > MAX_CHUNK_CHARS:
            # A big nested type becomes its own set of chunks.
            flush()
            _chunk_type(member, _qualify(namespace, type_name), chunks)
            continue

        if len(header) + len(member_text) > MAX_CHUNK_CHARS:
            flush()
            _emit_split(
                header, member_text + "\n}",
                _meta(namespace, type_name, member_name), chunks,
            )
            continue

        batch_size = len(header) + sum(len(t) for t in batch) + len(member_text)
        if batch and batch_size > MAX_CHUNK_CHARS:
            flush()
        batch.append(member_text)
        batch_names.append(member_name)
    flush()


def _emit_split(header, text, meta, chunks):
    """Last resort for one oversized piece: split on line borders."""
    budget = max(MAX_CHUNK_CHARS - len(header), 200)
    lines, piece = text.splitlines(keepends=True), ""
    for line in lines:
        if piece and len(piece) + len(line) > budget:
            chunks.append((header + piece, dict(meta)))
            piece = ""
        piece += line
    if piece.strip():
        chunks.append((header + piece, dict(meta)))


def _name_of(node):
    name = node.child_by_field_name("name")
    return name.text.decode("utf-8") if name is not None else ""


def _qualify(namespace, name):
    return f"{namespace}.{name}" if namespace and name else (name or namespace)


def _meta(namespace, type_name, members=""):
    meta = {"namespace": namespace, "type_name": type_name}
    if members:
        meta["members"] = members
    return meta
