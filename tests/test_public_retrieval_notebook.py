"""Offline application checks; no model credentials or external requests.

Run after installing the notebook dependencies:
    python -m unittest discover -s tests -p test_public_retrieval_notebook.py
"""
import json
from pathlib import Path
import unittest

from google.adk.models.base_llm import BaseLlm
from google.adk.models.llm_response import LlmResponse
from google.adk.tools.base_toolset import BaseToolset
from google.adk.tools.function_tool import FunctionTool
from google.genai import types
from pydantic import PrivateAttr


NOTEBOOK = (
    Path(__file__).resolve().parents[1]
    / 'chapter_notebooks/Chapter_14_Knowledge_Retrieval_(RAG_Parallel_MCP).ipynb'
)
SEED_URL = 'https://docs.python.org/3/library/venv.html'
# A distinct returned URL checks that the handoff retains query and fragment too.
RESULT_URL = SEED_URL + '?view=public#creating-virtual-environments'
EVIDENCE = 'Create a virtual environment with python -m venv .venv.'


def load_example():
    namespace = {}
    notebook = json.loads(NOTEBOOK.read_text())
    for cell in notebook['cells']:
        source = ''.join(cell['source'])
        if cell['cell_type'] == 'code' and 'QUESTION =' not in source:
            exec(compile(source, str(NOTEBOOK), 'exec'), namespace)
    return namespace


class DocumentationTools(BaseToolset):
    def __init__(self, fail=False):
        super().__init__()
        self.closed = False
        self.calls = []
        self.fail = fail

        async def web_search(objective: str, search_queries: list[str]) -> dict:
            """Search public documentation."""
            self.calls.append(('web_search', objective, search_queries))
            if self.fail:
                raise TimeoutError('fixture retrieval timeout')
            return {'url': RESULT_URL, 'title': 'venv documentation', 'excerpt': EVIDENCE}

        async def web_fetch(urls: list[str]) -> dict:
            """Fetch public documentation pages."""
            self.calls.append(('web_fetch', urls))
            return {'url': urls[0], 'title': 'venv documentation', 'excerpt': EVIDENCE}

        self.tools = [FunctionTool(web_search), FunctionTool(web_fetch)]

    async def get_tools(self, readonly_context=None):
        return self.tools

    async def close(self):
        self.closed = True


class ScriptedResearchModel(BaseLlm):
    """Exercise ADK's actual routing, tool calls, and session state handoffs."""
    model: str = 'offline_fixture'
    fail_writer: bool = False
    empty_writer: bool = False
    _stages: list = PrivateAttr(default_factory=list)

    async def generate_content_async(self, llm_request, stream=False):
        instruction = llm_request.config.system_instruction
        if 'Plan a short answer' in instruction:
            self._stages.append('plan')
            assert SEED_URL in str(llm_request.contents)
            part = types.Part(text=f'Verify venv creation. Start at {SEED_URL}. Query: Python venv create environment')
        elif 'Follow this plan:' in instruction:
            assert SEED_URL in instruction, 'The researcher did not receive the plan URL.'
            responses = [
                p.function_response
                for c in llm_request.contents for p in (c.parts or [])
                if p.function_response
            ]
            if not responses:
                part = types.Part(function_call=types.FunctionCall(
                    name='web_search',
                    args={'objective': 'Verify how Python venv creates isolated environments',
                          'search_queries': ['Python venv create environment']},
                ))
            else:
                response = responses[-1]
                url = response.response['url']
                if response.name == 'web_search':
                    part = types.Part(function_call=types.FunctionCall(
                        name='web_fetch', args={'urls': [url]},
                    ))
                else:
                    self._stages.append('evidence')
                    part = types.Part(text=f"{response.response['excerpt']} Source: {url}")
        elif 'Retrieved evidence:' in instruction:
            self._stages.append('answer')
            assert RESULT_URL in instruction, 'The writer lost the original returned URL.'
            assert EVIDENCE in instruction, 'The writer lost the supporting evidence.'
            if self.fail_writer:
                raise RuntimeError('fixture model failure')
            part = types.Part(text=' ' if self.empty_writer else f'{EVIDENCE} [Python docs]({RESULT_URL})')
        else:
            raise AssertionError('Unexpected application stage')
        yield LlmResponse(content=types.Content(role='model', parts=[part]))


class PublicRetrievalTests(unittest.IsolatedAsyncioTestCase):
    @classmethod
    def setUpClass(cls):
        cls.example = load_example()

    async def test_source_links_cross_real_adk_handoffs(self):
        tools = DocumentationTools()
        model = ScriptedResearchModel()
        state = await self.example['run_research'](
            f'How do I create a virtual environment? Start at {SEED_URL}', model, tools,
        )
        self.assertIn(SEED_URL, state['research_plan'])
        self.assertIn(RESULT_URL, state['research_notes'])
        self.assertIn(f']({RESULT_URL})', state['answer'])
        self.assertEqual(model._stages, ['plan', 'evidence', 'answer'])
        self.assertEqual([c[0] for c in tools.calls], ['web_search', 'web_fetch'])
        self.assertEqual(tools.calls[-1][1], [RESULT_URL])
        self.assertTrue(tools.closed)

    async def test_retrieval_failure_remains_visible_and_closes(self):
        tools = DocumentationTools(fail=True)
        with self.assertRaisesRegex(TimeoutError, 'fixture retrieval timeout'):
            await self.example['run_research'](f'Start at {SEED_URL}', ScriptedResearchModel(), tools)
        self.assertTrue(tools.closed)

    async def test_model_failure_remains_visible_and_closes(self):
        tools = DocumentationTools()
        with self.assertRaisesRegex(RuntimeError, 'fixture model failure'):
            await self.example['run_research'](
                f'Start at {SEED_URL}', ScriptedResearchModel(fail_writer=True), tools,
            )
        self.assertTrue(tools.closed)

    async def test_empty_answer_is_not_reported_as_success(self):
        tools = DocumentationTools()
        with self.assertRaisesRegex(RuntimeError, 'produced no answer'):
            await self.example['run_research'](
                f'Start at {SEED_URL}', ScriptedResearchModel(empty_writer=True), tools,
            )
        self.assertTrue(tools.closed)


if __name__ == '__main__':
    unittest.main()
