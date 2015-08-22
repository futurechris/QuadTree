using System;
using System.Collections.Generic;

namespace QuadTree
{
    public class QuadTree<T>
    {
        internal static Stack<Branch> branchPool = new Stack<Branch>();
        internal static Stack<Leaf> leafPool = new Stack<Leaf>();

        Branch root;
        internal int splitCount;
        internal Dictionary<T, Leaf> leafLookup = new Dictionary<T, Leaf>();

        public QuadTree(int splitCount, ref Quad region)
        {
            this.splitCount = splitCount;
            root = CreateBranch(this, null, ref region);
        }
        public QuadTree(int splitCount, Quad region)
            : this(splitCount, ref region)
        {

        }
        public QuadTree(int splitCount, float x, float y, float width, float height)
            : this(splitCount, new Quad(x, y, x + width, y + height))
        {

        }

        public void Clear()
        {
            root.Clear();
            root.Tree = this;
            leafLookup.Clear();
        }

        public static void ClearPools()
        {
            branchPool = new Stack<Branch>();
            leafPool = new Stack<Leaf>();
            Branch.tempPool = new Stack<List<Leaf>>();
        }

        public void Insert(T value, ref Quad quad)
        {
            Leaf leaf;
            if (!leafLookup.TryGetValue(value, out leaf))
            {
                leaf = CreateLeaf(value, ref quad);
                leafLookup.Add(value, leaf);
            }
            root.Insert(leaf);
        }
        public void Insert(T value, Quad quad)
        {
            Insert(value, ref quad);
        }
        public void Insert(T value, float x, float y, float width, float height)
        {
            var quad = new Quad(x, y, x + width, y + height);
            Insert(value, ref quad);
        }

        public bool SearchArea(ref Quad quad, ref List<T> values)
        {
            if (values != null)
                values.Clear();
            else
                values = new List<T>();
            root.SearchQuad(ref quad, values);
            return values.Count > 0;
        }
        public bool SearchArea(Quad quad, ref List<T> values)
        {
            return SearchArea(ref quad, ref values);
        }
        public bool SearchArea(float x, float y, float width, float height, ref List<T> values)
        {
            var quad = new Quad(x, y, x + width, y + height);
            return SearchArea(ref quad, ref values);
        }

        public bool FindCollisions(T value, ref List<T> values)
        {
            if (values != null)
                values.Clear();
            else
                values = new List<T>(leafLookup.Count);

            Leaf leaf;
            if (leafLookup.TryGetValue(value, out leaf))
            {
                var branch = leaf.Branch;

                //Add the leaf's siblings (prevent it from colliding with itself)
                if (branch.Leaves.Count > 0)
                    for (int i = 0; i < branch.Leaves.Count; ++i)
                        if (leaf != branch.Leaves[i] && leaf.Quad.Intersects(ref branch.Leaves[i].Quad))
                            values.Add(branch.Leaves[i].Value);

                //Add the branch's children
                if (branch.Split)
                    for (int i = 0; i < 4; ++i)
                        if (branch.Branches[i] != null)
                            branch.Branches[i].SearchQuad(ref leaf.Quad, values);

                //Add all leaves back to the root
                branch = branch.Parent;
                while (branch != null)
                {
                    if (branch.Leaves.Count > 0)
                        for (int i = 0; i < branch.Leaves.Count; ++i)
                            if (leaf.Quad.Intersects(ref branch.Leaves[i].Quad))
                                values.Add(branch.Leaves[i].Value);
                    branch = branch.Parent;
                }
            }
            return false;
        }

        public int CountBranches()
        {
            int count = 0;
            CountBranches(root, ref count);
            return count;
        }
        void CountBranches(Branch branch, ref int count)
        {
            ++count;
            if (branch.Split)
                for (int i = 0; i < 4; ++i)
                    if (branch.Branches[i] != null)
                        CountBranches(branch.Branches[i], ref count);
        }

        static Branch CreateBranch(QuadTree<T> tree, Branch parent, ref Quad quad)
        {
            var branch = branchPool.Count > 0 ? branchPool.Pop() : new Branch();
            branch.Tree = tree;
            branch.Parent = parent;
            branch.Split = false;
            float midX = quad.MinX + (quad.MaxX - quad.MinX) * 0.5f;
            float midY = quad.MinY + (quad.MaxY - quad.MinY) * 0.5f;
            branch.Quads[0].Set(quad.MinX, quad.MinY, midX, midY);
            branch.Quads[1].Set(midX, quad.MinY, quad.MaxX, midY);
            branch.Quads[2].Set(midX, midY, quad.MaxX, quad.MaxY);
            branch.Quads[3].Set(quad.MinX, midY, midX, quad.MaxY);
            return branch;
        }

        static Leaf CreateLeaf(T value, ref Quad quad)
        {
            var leaf = leafPool.Count > 0 ? leafPool.Pop() : new Leaf();
            leaf.Value = value;
            leaf.Quad = quad;
            return leaf;
        }

        internal class Branch
        {
            internal static Stack<List<Leaf>> tempPool = new Stack<List<Leaf>>();

            internal QuadTree<T> Tree;
            internal Branch Parent;
            internal Quad[] Quads = new Quad[4];
            internal Branch[] Branches = new Branch[4];
            internal List<Leaf> Leaves = new List<Leaf>();
            internal bool Split;

            internal void Clear()
            {
                Tree = null;
                Parent = null;
                Split = false;

                for (int i = 0; i < 4; ++i)
                {
                    if (Branches[i] != null)
                    {
                        branchPool.Push(Branches[i]);
                        Branches[i].Clear();
                        Branches[i] = null;
                    }
                }

                for (int i = 0; i < Leaves.Count; ++i)
                {
                    leafPool.Push(Leaves[i]);
                    Leaves[i].Branch = null;
                    Leaves[i].Value = default(T);
                }

                Leaves.Clear();
            }

            internal void Insert(Leaf leaf)
            {
                //If this branch is already split
                if (Split)
                {
                    //Check which quadrants the leaf intersects with
                    int index = -1;
                    for (int i = 0; i < 4; ++i)
                    {
                        if (leaf.Quad.Intersects(ref Quads[i]))
                        {
                            if (index < 0)
                                index = i;
                            else
                            {
                                //If it intersects with more than one quadrant, this branch owns it
                                Leaves.Add(leaf);
                                leaf.Branch = this;
                                return;
                            }
                        }
                    }
                    if (index >= 0)
                    {
                        //If it intsersects with only one quadrant, that quadrant owns it
                        if (Branches[index] == null)
                            Branches[index] = CreateBranch(Tree, this, ref Quads[index]);
                        Branches[index].Insert(leaf);
                    }
                    else
                    {
                        //If it intersects with nothing, return it to the pool
                        leafPool.Push(leaf);
                        Tree.leafLookup.Remove(leaf.Value);
                        leaf.Branch = null;
                        leaf.Value = default(T);
                    }
                }
                else
                {
                    //Add the leaf to this node
                    Leaves.Add(leaf);
                    leaf.Branch = this;

                    //Once I have reached capacity, split the node
                    if (Leaves.Count >= Tree.splitCount)
                    {
                        var temp = tempPool.Count > 0 ? tempPool.Pop() : new List<Leaf>();
                        temp.AddRange(Leaves);
                        Leaves.Clear();
                        Split = true;
                        for (int i = 0; i < temp.Count; ++i)
                            Insert(temp[i]);
                        temp.Clear();
                        tempPool.Push(temp);
                    }
                }
            }

            internal void SearchQuad(ref Quad quad, List<T> values)
            {
                if (Leaves.Count > 0)
                    for (int i = 0; i < Leaves.Count; ++i)
                        if (quad.Intersects(ref Leaves[i].Quad))
                            values.Add(Leaves[i].Value);
                for (int i = 0; i < 4; ++i)
                    if (Branches[i] != null)
                        Branches[i].SearchQuad(ref quad, values);
            }
        }

        internal class Leaf
        {
            internal Branch Branch;
            internal T Value;
            internal Quad Quad;
        }
    }

    public struct Quad
    {
        public float MinX;
        public float MinY;
        public float MaxX;
        public float MaxY;

        public Quad(float minX, float minY, float maxX, float maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public void Set(float minX, float minY, float maxX, float maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public bool Intersects(ref Quad other)
        {
            return MinX < other.MaxX && MinY < other.MaxY && MaxX > other.MinX && MaxY > other.MinY;
        }
    }
}

