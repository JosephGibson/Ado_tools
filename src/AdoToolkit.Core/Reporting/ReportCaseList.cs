using System.Collections;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Reporting;

// Builds each case model on access and keeps none, so renderers hold one case body at a time.
internal sealed class ReportCaseList(IReadOnlyList<AdoTestCase> source, Func<AdoTestCase, TestCaseReportModel> build)
    : IReadOnlyList<TestCaseReportModel>
{
    public int Count => source.Count;

    public TestCaseReportModel this[int index] => build(source[index]);

    public IEnumerator<TestCaseReportModel> GetEnumerator()
    {
        for (int index = 0; index < source.Count; index++) yield return build(source[index]);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
