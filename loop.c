int main() {
    printf("Fibonacci Sequence:\n");
    int n = 10;
    int t1 = 0;
    int t2 = 1;
    
    for (int i = 1; i <= n; i++) {
        printf("%d ", t1);
        int nextTerm = t1 + t2;
        t1 = t2;
        t2 = nextTerm;
    }
    printf("\nFinished!\n");
    return 0;
}