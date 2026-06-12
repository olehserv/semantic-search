"""Unit tests for the code-aware C# chunker in scripts/chunking.py.

Skip when tree-sitter is not installed (CI runs without the indexing
stack, same pattern as the numpy/llama-index skips).
"""
import pytest

pytest.importorskip("tree_sitter_c_sharp")

from chunking import MAX_CHUNK_CHARS, chunk_csharp


SMALL_CLASS = """using System;

namespace Shop.Orders
{
    public class OrderService : IOrderService
    {
        private readonly IRepo _repo;

        public OrderService(IRepo repo) { _repo = repo; }

        public void Place(Order order)
        {
            _repo.Save(order);
        }
    }
}
"""


def big_class(methods=12, body_lines=20):
    body = "\n".join("            var x{0} = {0};".format(i) for i in range(body_lines))
    members = "\n".join(
        f"        public void Method{i}()\n        {{\n{body}\n        }}\n"
        for i in range(methods)
    )
    return (
        "namespace Big.App\n{\n"
        f"    public class Huge : IBase\n    {{\n{members}    }}\n}}\n"
    )


def test_small_class_is_one_chunk_with_metadata():
    chunks = chunk_csharp(SMALL_CLASS)
    assert len(chunks) == 1
    text, meta = chunks[0]
    assert meta["namespace"] == "Shop.Orders"
    assert meta["type_name"] == "OrderService"
    assert "// namespace Shop.Orders" in text
    assert "class OrderService" in text
    assert "_repo.Save(order);" in text  # body kept with signature


def test_big_class_splits_on_member_borders():
    chunks = chunk_csharp(big_class())
    assert len(chunks) > 1
    for text, meta in chunks:
        assert len(text) <= MAX_CHUNK_CHARS + 200  # header tolerance
        assert meta["type_name"] == "Huge"
        # The type signature rides on every chunk.
        assert "class Huge : IBase" in text
        # Method bodies never get torn from their signatures: every chunk
        # has matching numbers of signature and body openers.
        assert text.count("public void Method") == text.count("var x0 =")
    all_members = ",".join(meta["members"] for _, meta in chunks)
    assert "Method0" in all_members and "Method11" in all_members


def test_file_scoped_namespace_applies_to_siblings():
    chunks = chunk_csharp("namespace A.B;\n\npublic record R(int X);\n")
    assert len(chunks) == 1
    text, meta = chunks[0]
    assert meta["namespace"] == "A.B"
    assert meta["type_name"] == "R"


def test_enum_and_interface_are_chunked():
    src = """namespace N
{
    public interface IThing { void Do(); }
    public enum Color { Red, Green }
}
"""
    chunks = chunk_csharp(src)
    names = {meta["type_name"] for _, meta in chunks}
    assert names == {"IThing", "Color"}


def test_unparsable_source_returns_none_for_fallback():
    assert chunk_csharp("this is not C# at all {{{") is None


def test_oversized_single_method_is_line_split_with_header():
    body = "\n".join(f"            sum += {i};" for i in range(400))
    src = (
        "namespace N\n{\n    public class C\n    {\n"
        f"        public int Sum()\n        {{\n{body}\n        }}\n"
        "    }\n}\n"
    )
    chunks = chunk_csharp(src)
    assert len(chunks) > 1
    for text, meta in chunks:
        assert meta["type_name"] == "C"
        assert "class C" in text  # header survives the line split
