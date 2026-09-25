namespace Brain.Data.Collection.CSR;

public partial class CsrArray<T>
{
    private void Quicksort(int min, int max)
    {
        while (true) // alleviate an entire branch of recursion
        {
            // fall back to insertion sort for small intervals to lessen recursive overhead
            if (max - min < QuicksortCutoff)
            {
                InsertionSort(min, max);
                return;
            }

            var pivotIndex = Partition(min, max);

            Quicksort(min, pivotIndex - 1);
            min = pivotIndex + 1;
        }
    }

    private int Partition(int min, int max)
    {
        var mid = min + (max - min) / 2;

        Swap(mid, max);

        var pivot = _pending[max];
        var i     = min - 1;

        for (var j = min; j < max; j++)
        {
            if (_pending[j] >= pivot)
                continue;

            i++;
            Swap(i, j);
        }

        Swap(i + 1, max);

        return i + 1;

        void Swap(int k, int l)
        {
            if (k == l) return;
            (_pending[k], _pending[l]) = (_pending[l], _pending[k]);
        }
    }

    private void InsertionSort(int min, int max)
    {
        for (var i = min + 1; i <= max; i++)
        {
            var current = _pending[i];
            var j       = i - 1;

            while (j >= min && _pending[j] > current)
            {
                _pending[j + 1] = _pending[j];
                j--;
            }

            _pending[j + 1] = current;
        }
    }
}