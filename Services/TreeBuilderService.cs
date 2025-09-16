using BlazorApp_ProductosAPI.Models;

namespace BlazorApp_ProductosAPI.Services;

public class TreeBuilderService
{
    public List<TreeNode> BuildTree(List<UbicacionItem> items)
    {
        var nodeDict = new Dictionary<string, TreeNode>();
        var rootNodes = new List<TreeNode>();

        // Crear todos los nodos
        foreach (var item in items.OrderBy(x => x.Orden))
        {
            var node = new TreeNode
            {
                Item = item,
                Level = GetLevel(item.Orden)
            };
            nodeDict[item.Orden] = node;
        }

        // Construir jerarquía
        foreach (var node in nodeDict.Values)
        {
            var parentKey = GetParentKey(node.Item.Orden);
            
            if (string.IsNullOrEmpty(parentKey))
            {
                // Es un nodo raíz
                rootNodes.Add(node);
            }
            else if (nodeDict.ContainsKey(parentKey))
            {
                // Tiene padre
                var parent = nodeDict[parentKey];
                parent.Children.Add(node);
                node.Parent = parent;
            }
            else
            {
                // Padre no encontrado, tratarlo como raíz
                rootNodes.Add(node);
            }
        }

        // Ordenar hijos por orden
        SortChildrenRecursively(rootNodes);

        return rootNodes;
    }

    private int GetLevel(string orden)
    {
        return orden.Split('.').Length - 1;
    }

    private string GetParentKey(string orden)
    {
        var parts = orden.Split('.');
        if (parts.Length <= 1)
            return string.Empty;
        
        return string.Join(".", parts.Take(parts.Length - 1));
    }

    private void SortChildrenRecursively(List<TreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.Children = node.Children.OrderBy(x => x.Item.Orden).ToList();
            SortChildrenRecursively(node.Children);
        }
    }

    public void ApplySearch(List<TreeNode> rootNodes, string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            SetAllVisible(rootNodes, true);
            return;
        }

        var term = searchTerm.ToLowerInvariant();
        SetAllVisible(rootNodes, false);
        
        foreach (var root in rootNodes)
        {
            ApplySearchRecursive(root, term);
        }
    }

    private bool ApplySearchRecursive(TreeNode node, string searchTerm)
    {
        var matches = node.Item.Descripcion.ToLowerInvariant().Contains(searchTerm);
        
        foreach (var child in node.Children)
        {
            if (ApplySearchRecursive(child, searchTerm))
            {
                matches = true;
            }
        }

        if (matches)
        {
            node.IsVisible = true;
            // Hacer visibles todos los ancestros
            var parent = node.Parent;
            while (parent != null)
            {
                parent.IsVisible = true;
                parent.IsExpanded = true;
                parent = parent.Parent;
            }
        }

        return matches;
    }

    private void SetAllVisible(List<TreeNode> nodes, bool visible)
    {
        foreach (var node in nodes)
        {
            node.IsVisible = visible;
            SetAllVisible(node.Children, visible);
        }
    }
}
