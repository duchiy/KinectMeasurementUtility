using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WpfApplication1
{
    public class ConvolutionMatrix
    {

        public int Factor { get; set; }
        public int Offset { get; set; }

        private int[,] _matrix = {  {0, 0, 0, 0, 0}, 
                                    {0, 0, 0, 0, 0}, 
                                    {0, 0, 1, 0, 0}, 
                                    {0, 0, 0, 0, 0}, 
                                    {0, 0, 0, 0, 0} 
                                };

        public int[,] Matrix
        {
            get { return _matrix; }
            set
            {
                _matrix = value;

                Factor = 0;
                for (int i = 0; i < Size; i++)
                    for (int j = 0; j < Size; j++)
                        Factor += _matrix[i, j];

                if (Factor == 0)
                    Factor = 1;
            }
        }




        private int _size = 5;
        public int Size
        {
            get { return _size; }
            set
            {
                if (value != 1 && value != 3 && value != 5 && value != 7)
                    _size = 5;
                else
                    _size = value;
            }
        }


        public ConvolutionMatrix()
        {
            Offset = 0;
            Factor = 1;
        }


    } 
}
