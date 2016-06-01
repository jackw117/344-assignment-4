using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text;
using System.IO;

namespace ClassLibrary1
{
    public class Trie
    {
        public Node root { get; private set; }
        private char end = '$';
        public List<string> words { get; private set; }

        public Trie()
        {
            root = new Node();
        }

        public void addWord(string word)
        {
            word = word.ToLower() + end;
            var currentNode = root;
            foreach (char ch in word)
            {
                int val = values(ch);
                currentNode = currentNode.addChild(ch, val);
            }
        }

        public List<string> checkWord(string word)
        {
            words = new List<string>();
            var currentNode = root;
            foreach (char ch in word)
            {
                int val = values(ch);
                if (currentNode.Children[val] != null)
                {
                    currentNode = (Node)currentNode.Children[val];
                }
                else
                {
                    words.Add("No results");
                    return words;
                }
            }
            word = word.Substring(0, word.Length - 1);
            currentNode = search(word, currentNode);
            return words;
        }

        private Node search(string word, Node current)
        {
            if (words.Count != 10)
            {
                word += current.currentChar;
                foreach (int key in current.Children.Keys)
                {
                    if (key == values(end))
                    {
                        if (words.Count < 10)
                        {
                            words.Add(word);
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        search(word, (Node)current.Children[key]);
                    }
                }
            }
            return current;
        }

        private int values(char ch)
        {
            if (ch == ' ')
            {
                return 26;
            }
            else if (ch == end)
            {
                return 27;
            }
            else
            {
                return ((int)ch) - 97;
            }
        }
    }
}