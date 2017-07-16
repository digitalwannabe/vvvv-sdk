#pragma once

#include <stdexcept>
using namespace std;

namespace BooleanMesh
{
	extern "C" { __declspec(dllexport) int * meshBoolean(
		char * operationString, 
		double *VerticesA, 
		int *IndicesA, 
		double *VerticesB, 
		int *IndicesB, 
		int * binSizes); }//, int *PEext);}
	extern "C" { __declspec(dllexport) void getValues(double * verticesOut,int * indicesOut); }
//	extern "C" { __declspec(dllexport) double* matrix_multiplication(double *b_in, double *c_in, int *matSizes);}
	extern "C" __declspec(dllexport) int ReleaseMemory(double* pArray)
	{
		delete[] pArray;
		//delete[] Usize;
		return 0;
	}
}

