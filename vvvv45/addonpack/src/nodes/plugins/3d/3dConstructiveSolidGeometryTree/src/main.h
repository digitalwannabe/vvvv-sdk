#pragma once

#include <stdexcept>
using namespace std;

namespace ComputeCSG
{
	extern "C" { __declspec(dllexport) int* ComputeCSG(
		char * operationString, 
		double *allVerticesIn, 
		int *allIndicesIn, 
		int *verticesCount, 
		int *indicesCount, 
		int * verticesShift, 
		int * indicesShift, 
		int *binSizes); }//, int *PEext);}
	extern "C" { __declspec(dllexport) void getValues(double * verticesOut, int * indicesOut, int * refOut); }
//	extern "C" { __declspec(dllexport) double* forward_kinematics(double *dQ, double *dT, double *Cext, int *BEext, int *binSizes); }
//	extern "C" { __declspec(dllexport) double* matrix_multiplication(double *b_in, double *c_in, int *matSizes);}
	extern "C" __declspec(dllexport) int ReleaseMemory(double* pArray)
	{
		delete[] pArray;
		//delete[] Usize;
		return 0;
	}
}

