using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static MyLibrary.MyScheduler;

namespace MyLibrary
{
    /// <summary>
    /// Klasa koja predstavlja graf koji sluzi za detekciju deadlock-a.
    /// </summary>
    public class Graph
    {
        private readonly int maxDimension=15;
        // lista cvorova
        private List<Object> vertices = new List<Object>();

        // matrica susjednosti
        private bool[,] AdjacencyMatrix { get; set; }  //ovdje cuvam grane

        // konstruktor
        public Graph()
        {
            AdjacencyMatrix= new bool[maxDimension,maxDimension ];
        }

        // metoda koja dodaje granu u graf
        internal void AddEdge(Object source, Object destination)
        {
            int srcIndex = vertices.IndexOf(source);
            int dstIndex = vertices.IndexOf(destination);

            if (srcIndex == -1)  //ne postoji ovaj cvor
            {
                vertices.Add(source);
                srcIndex = vertices.IndexOf(source);
            }
            if (dstIndex == -1)
            {
                vertices.Add(destination);
                dstIndex = vertices.IndexOf(destination);
            }

            AdjacencyMatrix[srcIndex, dstIndex] = true;
        }

        // metoda koja uklanja granu iz grafa
        internal void RemoveEdge(Object source, Object destination)
        {
            int srcIndex = vertices.IndexOf(source);
            int dstIndex = vertices.IndexOf(destination);

            if (srcIndex==-1 || dstIndex == -1)
            {
                return;
            }

            AdjacencyMatrix[srcIndex, dstIndex] = false;
        }

        // metoda koja vraca susjede cvora vertex
        internal List<Object> Neighbours(Object vertex)
        {
            var neighbors = new List<Object>();
            int source = vertices.IndexOf(vertex);

            if (source != -1)
                for (int adjacent = 0; adjacent < vertices.Count; ++adjacent)
                    if (vertices[adjacent] != null && DoesEdgeExist(source, adjacent))
                        neighbors.Add(vertices[adjacent]);

            return neighbors;
        }
    
        // metoda koja provjerava da li grana izmedju cvorova sa indeksima source i destination postoji
        private bool DoesEdgeExist(int source, int destination)
        {
            return (AdjacencyMatrix[source, destination] == true);
        }

        // metoda koja provjerava da li u grafu postoji deadlock
        internal static bool CheckDeadlock(Graph graph, Object source, ref HashSet<Object> visited, ref HashSet<Object> recursionStack)
        {
            if (!visited.Contains(source))
            {
                visited.Add(source);
                recursionStack.Add(source);
      
                foreach (var adjacent in graph.Neighbours(source))
                {                
                    if (!visited.Contains(adjacent) && CheckDeadlock(graph, adjacent, ref visited, ref recursionStack))
                        return true;
                    if (recursionStack.Contains(adjacent))
                        return true;
                }
            }
            recursionStack.Remove(source);
            return false;
        }


        // metoda koja predstavlja dio logike elementarnog razrjesavanje deadlock-a
        // uklanja sve grane koje poticu od i ka zadatku koji je prouzrokovao deadlock
        internal void DeadlockSolver(Object task)
        {
            
            int index = vertices.IndexOf(task);
            var neighbours = Neighbours(task);
            foreach (var vertex in neighbours)
            {
                RemoveEdge(task, vertex);
            }
            foreach (var vertex in vertices)
            {
                if (vertex is Task) continue;
                Resource resource = (Resource)vertex;
                Task owner = Resource.GetOwner(resource);

                if (owner == null || owner != task) continue;

                Resource.UnlockResource((Task)task, resource);
            }
       }
    }
}
