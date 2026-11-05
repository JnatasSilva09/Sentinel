namespace Sentinel.App;

internal static class Grades
{
    public static DataGridView Criar()
    {
        var grade = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = SystemColors.Window, // sem o cinza escuro de fundo quando a tabela tem poucas linhas
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        grade.CellFormatting += PintarPorGravidade;
        return grade;
    }

    public static DataGridViewTextBoxColumn Coluna(string propriedade, string titulo, float peso, string? formato = null)
    {
        var coluna = new DataGridViewTextBoxColumn { DataPropertyName = propriedade, HeaderText = titulo, FillWeight = peso };
        if (formato is not null) coluna.DefaultCellStyle.Format = formato;
        return coluna;
    }

    /// <summary>Recoloca a seleção na linha cujo Id é o informado (após recarregar a tabela).</summary>
    public static void SelecionarPorId<T>(DataGridView grade, int? id, Func<T, int> obterId) where T : class
    {
        if (id is null) return;
        foreach (DataGridViewRow linha in grade.Rows)
        {
            if (linha.DataBoundItem is T item && obterId(item) == id.Value)
            {
                linha.Selected = true;
                grade.CurrentCell = linha.Cells[0];
                return;
            }
        }
    }

    // A cor ajuda a bater o olho, mas a gravidade também aparece em texto (acessibilidade).
    private static void PintarPorGravidade(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || sender is not DataGridView grade) return;
        if (e.CellStyle is null) return;
        if (grade.Rows[e.RowIndex].DataBoundItem is not ILinhaComGravidade linha) return;

        if (linha is LinhaIncidente { Resolvido: true })
        {
            e.CellStyle.BackColor = Color.FromArgb(245, 245, 245);
            e.CellStyle.ForeColor = Color.FromArgb(110, 110, 110);
            return;
        }

        e.CellStyle.ForeColor = Color.Black;
        e.CellStyle.BackColor = Cores.Fundo(linha.Gravidade);
    }
}
