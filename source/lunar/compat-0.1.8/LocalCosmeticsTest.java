import com.google.protobuf.*;
import com.lunarclient.common.v1.Uuid;
import com.lunarclient.websocket.cosmetic.v2.*;
import com.lunarclient.websocket.badge.v1.BadgeService;
import com.lunarclient.websocket.badge.v1.EquipBadgeRequest;
import java.nio.file.*;

public class LocalCosmeticsTest {
    private static int calls;
    private static LoginResponse received;
    private static Outfit created;
    private static Uuid id(long n) { return Uuid.newBuilder().setHigh64(n).setLow64(n).build(); }
    public static void main(String[] args) throws Exception {
        System.setProperty("lunar.localCosmeticsDir", args[0]);
        final LoginResponse server = LoginResponse.newBuilder()
            .addOutfits(Outfit.newBuilder().setId(id(11)).setName("Online")
                .addCosmetics(EquippedCosmetic.newBuilder().setCosmeticId(7)))
            .setOutfitTree(OutfitTree.newBuilder().setDefaultOutfitId(id(11))).build();
        RpcChannel channel = new RpcChannel() {
            public void callMethod(Descriptors.MethodDescriptor method, RpcController controller,
                Message request, Message response, RpcCallback<Message> callback) {
                calls++;
                if (!method.getName().equals("Login")) throw new AssertionError("Outgoing mutation: " + method.getName());
                callback.run(server);
            }
        };
        CosmeticService.Interface stub = CosmeticService.newStub(channel);
        stub.login(null, LoginRequest.getDefaultInstance(), value -> received = value);
        assert received.getHasAllCosmeticsFlag();
        assert received.getOutfitsCount() == 1;
        Outfit local = received.getOutfits(0);
        assert !local.getId().equals(id(11));
        assert local.getCosmeticsCount() == 0;
        Outfit changed = local.toBuilder().addCosmetics(EquippedCosmetic.newBuilder().setCosmeticId(12345)).build();
        stub.updateOutfit(null, UpdateOutfitRequest.newBuilder().setOutfit(changed).build(), value -> {
            assert value.getStatus() == UpdateOutfitResponse.Status.STATUS_OK;
            assert value.getOutfit().getCosmetics(0).getCosmeticId() == 12345;
        });
        stub.createOutfit(null, CreateOutfitRequest.newBuilder().setName("Second").build(), value -> {
            assert value.getStatus() == CreateOutfitResponse.Status.STATUS_OK;
            created = value.getOutfit();
        });
        stub.selectOutfit(null, SelectOutfitRequest.newBuilder().setOutfitTree(
            OutfitTree.newBuilder().setDefaultOutfitId(local.getId())).build(), value -> {
            assert value.getStatus() == SelectOutfitResponse.Status.STATUS_OK;
        });
        stub.deleteOutfit(null, DeleteOutfitRequest.newBuilder().setOutfitId(created.getId()).build(), value -> {
            assert value.getStatus() == DeleteOutfitResponse.Status.STATUS_OK;
        });
        stub.favoriteCosmetic(null, FavoriteCosmeticRequest.getDefaultInstance(), value -> { assert value != null; });
        stub.unfavoriteCosmetic(null, UnfavoriteCosmeticRequest.getDefaultInstance(), value -> { assert value != null; });
        BadgeService.newStub(channel).equipBadge(null, EquipBadgeRequest.getDefaultInstance(), value -> { assert value != null; });
        assert calls == 1 : "Cosmetic mutation reached the channel";
        stub.login(null, LoginRequest.getDefaultInstance(), value -> received = value);
        assert calls == 2;
        assert received.getOutfitsCount() == 1;
        assert received.getOutfits(0).getCosmetics(0).getCosmeticId() == 12345;
        assert received.getOutfitTree().getDefaultOutfitId().equals(local.getId());
        assert server.getOutfits(0).getCosmetics(0).getCosmeticId() == 7;
        Path file = Files.list(Paths.get(args[0])).filter(p -> p.toString().endsWith(".pb")).findFirst().get();
        Files.write(file, new byte[] {0, 0, 0});
        boolean rejected = false;
        try { stub.login(null, LoginRequest.getDefaultInstance(), value -> received = value); }
        catch (IllegalStateException expected) { rejected = true; }
        assert rejected : "Damaged local state should fail closed";
        System.out.println("PASS: patched stubs, local unlock, outfit CRUD, persistence, no mutation RPCs, damaged state rejection.");
    }
}
