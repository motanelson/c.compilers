int main() {
    printf("Executing program...\n");

    int total = 0;
    for (int i = 1; i <= 5; i++) {
        total = total + i;
        printf("Index %d -> Running total: %d\n", i, total);
    }

    if (total == 15) {
        printf("\nResult matches standard sum formula (n*(n+1)/2 = %d)\n", total);
    }

    return 0;
}