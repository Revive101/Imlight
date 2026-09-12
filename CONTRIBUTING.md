# Contributing to Imlight
 
Thank you for your interest in contributing to Imlight. This document sets out the requirements and expectations for submitting issues and pull requests.
Contributions that do not meet these guidelines may be closed without extended discussion.

## 1. General Principles
1.1. Imlight is maintained by volunteers (with limited time). These guidelines exist to keep that time focused on contributions that are reviewable, reproducible, and understood by the person submitting them.

1.2. Contributors are expected to understand and be able to explain any code, report, or documentation they submit, regardless of how it was produced.
 
1.3. Maintainers reserve the right to close, request changes to, or decline any issue or pull request that does not meet the standards below.

## 2. Use of AI Tools
2.1. **Disclosure is required.** If an issue or pull request was generated, drafted, or substantially assisted by an AI tool (including but not limited to code assistants, chatbots, or automated agents),
this must be disclosed at the time of submission using the field provided in the issue or pull request template.

2.2. AI-assisted contributions are not prohibited. However, the contributor remains fully responsible for the accuracy, correctness, and quality of the submission, whether or not AI tools were used in producing it.

2.3. A contributor must be able to answer follow-up questions in their own words about their submission, including the reasoning behind a proposed change, how it was tested, and why a reported bug occurs.
Inability to do so may result in the submission being closed.

2.4. Submissions that show clear signs of being unreviewed AI output (for example: References to code or files that do not exist in the repository,
or explanations that contradict the submitted diff) will be closed as invalid.

2.5. Failure to disclose AI assistance when later determined to be present may result in the submission being closed and restrictions on further contributions.

## 3. Submitting an Issue
3.1. Before opening an issue, search existing open and closed issues to confirm it has not already been reported.

3.2. All issues must use the appropriate issue template and include:
- A clear, specific title.
- A description of the expected behavior and the actual behavior.
- Steps to reproduce the issue, sufficient for a maintainer to reproduce it independently.
- Relevant environment details (e.g. OS, Imlight version/commit, configuration relevant to the issue).
- Logs, stack traces, or screenshots where applicable.
- Disclosure of any AI assistance used in preparing the report, per Section 2.

3.3. Issues lacking reproduction steps or sufficient detail to act on may be labeled "needs information" and closed if no response is received within a reasonable period.

3.4. Feature requests should describe the problem being solved, not only the proposed solution, so that maintainers can evaluate alternatives.

## 4. Submitting a Pull Request
4.1. Non-trivial pull requests (anything beyond a typo fix or comparably small change) should reference an existing, discussed issue.
Opening a pull request without prior discussion for a substantial change is done at the contributor's own risk of rejection.

4.2. All pull requests must use the pull request template and include:
- A description of what the change does and why it is needed.
- Disclosure of any AI assistance used in producing the change, per Section 2.
- Reference to the related issue, where applicable (e.g. `Closes #123`).

4.3. Pull requests must:
- Pass all CI checks before being considered for review.
- Follow the existing code style and conventions of the affected part of the codebase.
- Include or update tests where the codebase's testing conventions call for them.

4.4. Large, unsolicited pull requests (e.g. broad refactors, dependency overhauls, or architectural changes) without prior discussion in an issue will typically be closed and redirected to a discussion first.

4.5. Maintainers may request changes, ask clarifying questions, or close a pull request that does not meet these standards.
Contributors are expected to respond to review feedback in a reasonable timeframe. Stale pull requests may be closed.

## 5. Code of Conduct
5.1. All contributors are expected to engage respectfully with maintainers and other contributors. Disagreement with a maintainer's decision should be raised civilly in the relevant issue or pull request.


## 6. Licensing
6.1. By submitting a contribution, you agree that it is licensed under the same terms as the rest of the Imlight project, and that you have the right to submit it under those terms.

---

Questions about these guidelines can be raised by opening a discussion or issue tagged accordingly.
