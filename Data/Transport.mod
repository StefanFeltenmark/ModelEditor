int N = 4;
int M = 4;

range I = 1..N;
range J = 1..M;

float c[I,J] = ...;

dvar float+ x[I,J];

minimize sum(i in I) sum(j in J) c[i,j]*x[i,j];

subject to
{
 forall(j in J)
   sum(i in I) x[i,j] == 1;

 forall(i in I)
   sum(j in J) x[i,j] == 1;


}

